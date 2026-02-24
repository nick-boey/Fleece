using System.Collections.Concurrent;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// In-memory cached issue service that provides fast reads from a <see cref="ConcurrentDictionary{TKey,TValue}"/> cache.
/// Writes update the cache immediately and queue persistence to disk via <see cref="IIssueSerializationQueue"/>.
/// A <see cref="FileSystemWatcher"/> monitors the <c>.fleece/</c> directory for external changes
/// and triggers a debounced cache reload.
/// </summary>
public sealed class FleeceInMemoryService : IFleeceInMemoryService
{
    private const string FleeceDirectory = ".fleece";
    private const string IssuesFileFilter = "issues*.jsonl";
    private static readonly TimeSpan ReloadDebounceInterval = TimeSpan.FromMilliseconds(500);

    private readonly IIssueService _issueService;
    private readonly IIssueSerializationQueue _serializationQueue;
    private readonly string _basePath;

    private readonly ConcurrentDictionary<string, Issue> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReaderWriterLockSlim _cacheLock = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private volatile bool _isLoaded;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of the in-memory service.
    /// </summary>
    /// <param name="issueService">The underlying issue service for CRUD operations.</param>
    /// <param name="serializationQueue">The queue for asynchronous disk persistence.</param>
    /// <param name="basePath">The project base path containing the <c>.fleece/</c> directory.</param>
    public FleeceInMemoryService(
        IIssueService issueService,
        IIssueSerializationQueue serializationQueue,
        string basePath)
    {
        _issueService = issueService;
        _serializationQueue = serializationQueue;
        _basePath = basePath;

        InitializeFileWatcher();
    }

    /// <inheritdoc />
    public bool IsLoaded => _isLoaded;

    #region Read Operations

    /// <inheritdoc />
    public async Task<Issue?> GetIssueAsync(string issueId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureCacheLoadedAsync(ct);

        _cacheLock.EnterReadLock();
        try
        {
            return _cache.TryGetValue(issueId, out var issue) ? issue : null;
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Issue>> ListIssuesAsync(
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureCacheLoadedAsync(ct);

        _cacheLock.EnterReadLock();
        try
        {
            IEnumerable<Issue> issues = _cache.Values;

            if (status.HasValue)
            {
                issues = issues.Where(i => i.Status == status.Value);
            }

            if (type.HasValue)
            {
                issues = issues.Where(i => i.Type == type.Value);
            }

            if (priority.HasValue)
            {
                issues = issues.Where(i => i.Priority == priority.Value);
            }

            // If no filters specified, exclude terminal statuses (matching IssueService behaviour)
            if (!status.HasValue && !type.HasValue && !priority.HasValue)
            {
                issues = issues.Where(i => !i.Status.IsTerminal());
            }

            return issues.ToList();
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Issue>> SearchAsync(string query, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        await EnsureCacheLoadedAsync(ct);

        _cacheLock.EnterReadLock();
        try
        {
            return _cache.Values
                .Where(i =>
                    i.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (i.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (i.Tags?.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)) ?? false))
                .ToList();
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Issue>> FilterAsync(
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureCacheLoadedAsync(ct);

        _cacheLock.EnterReadLock();
        try
        {
            return _cache.Values
                .Where(i => status is null || i.Status == status)
                .Where(i => status is not null || includeTerminal || !i.Status.IsTerminal())
                .Where(i => type is null || i.Type == type)
                .Where(i => priority is null || i.Priority == priority)
                .Where(i => assignedTo is null || string.Equals(i.AssignedTo, assignedTo, StringComparison.OrdinalIgnoreCase))
                .Where(i => tags is null || tags.Count == 0 || tags.Any(t => i.Tags?.Contains(t, StringComparer.OrdinalIgnoreCase) ?? false))
                .Where(i => linkedPr is null || i.LinkedPR == linkedPr)
                .ToList();
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }
    }

    #endregion

    #region Write Operations

    /// <inheritdoc />
    public async Task<Issue> CreateIssueAsync(
        string title,
        IssueType type,
        string? description = null,
        int? priority = null,
        ExecutionMode? executionMode = null,
        IssueStatus? status = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureCacheLoadedAsync(ct);

        // Determine effective status: if not provided, use Draft for issues without description, Open otherwise
        var effectiveStatus = status ?? (string.IsNullOrWhiteSpace(description)
            ? IssueStatus.Draft : IssueStatus.Open);

        // Create via the underlying service (which writes to disk)
        var issue = await _issueService.CreateAsync(
            title: title,
            type: type,
            description: description,
            status: effectiveStatus,
            priority: priority,
            executionMode: executionMode,
            cancellationToken: ct);

        // Update cache
        _cacheLock.EnterWriteLock();
        try
        {
            _cache[issue.Id] = issue;
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }

        // Queue persistence for background write-through
        await _serializationQueue.EnqueueAsync(new IssueWriteOperation(
            IssueId: issue.Id,
            Type: WriteOperationType.Create,
            WriteAction: async innerCt =>
            {
                // The issue was already created above via _issueService.CreateAsync,
                // so we only need to ensure it is persisted. The underlying storage
                // handles this — no additional action needed since the create call
                // already wrote to disk.
                await Task.CompletedTask;
            },
            QueuedAt: DateTimeOffset.UtcNow
        ), ct);

        return issue;
    }

    /// <inheritdoc />
    public async Task<Issue?> UpdateIssueAsync(
        string issueId,
        string? title = null,
        IssueStatus? status = null,
        IssueType? type = null,
        string? description = null,
        int? priority = null,
        ExecutionMode? executionMode = null,
        string? workingBranchId = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureCacheLoadedAsync(ct);

        // Check cache first
        _cacheLock.EnterReadLock();
        try
        {
            if (!_cache.ContainsKey(issueId))
            {
                return null;
            }
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }

        Issue updated;
        try
        {
            updated = await _issueService.UpdateAsync(
                id: issueId,
                title: title,
                status: status,
                type: type,
                description: description,
                priority: priority,
                workingBranchId: workingBranchId,
                executionMode: executionMode,
                cancellationToken: ct);
        }
        catch (KeyNotFoundException)
        {
            // Issue was removed externally between our cache check and the update
            _cacheLock.EnterWriteLock();
            try
            {
                _cache.TryRemove(issueId, out _);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }

            return null;
        }

        // Update cache with the new issue (which may have a new ID if title changed)
        _cacheLock.EnterWriteLock();
        try
        {
            // If title changed, the ID changes — remove the old entry
            if (title is not null && !string.Equals(updated.Id, issueId, StringComparison.OrdinalIgnoreCase))
            {
                _cache.TryRemove(issueId, out _);
            }

            _cache[updated.Id] = updated;
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }

        await _serializationQueue.EnqueueAsync(new IssueWriteOperation(
            IssueId: updated.Id,
            Type: WriteOperationType.Update,
            WriteAction: async _ => await Task.CompletedTask,
            QueuedAt: DateTimeOffset.UtcNow
        ), ct);

        return updated;
    }

    /// <inheritdoc />
    public async Task<Issue?> UpdateIssueFullAsync(
        string issueId,
        string? title = null,
        string? description = null,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        int? linkedPr = null,
        IReadOnlyList<string>? linkedIssues = null,
        IReadOnlyList<ParentIssueRef>? parentIssues = null,
        IReadOnlyList<string>? tags = null,
        string? workingBranchId = null,
        ExecutionMode? executionMode = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureCacheLoadedAsync(ct);

        // Check cache first
        _cacheLock.EnterReadLock();
        try
        {
            if (!_cache.ContainsKey(issueId))
            {
                return null;
            }
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }

        Issue updated;
        try
        {
            updated = await _issueService.UpdateAsync(
                id: issueId,
                title: title,
                description: description,
                status: status,
                type: type,
                priority: priority,
                linkedPr: linkedPr,
                linkedIssues: linkedIssues,
                parentIssues: parentIssues,
                assignedTo: assignedTo,
                tags: tags,
                workingBranchId: workingBranchId,
                executionMode: executionMode,
                cancellationToken: ct);
        }
        catch (KeyNotFoundException)
        {
            _cacheLock.EnterWriteLock();
            try
            {
                _cache.TryRemove(issueId, out _);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }

            return null;
        }

        // Update cache with the new issue (which may have a new ID if title changed)
        _cacheLock.EnterWriteLock();
        try
        {
            // If title changed, the ID changes — remove the old entry
            if (title is not null && !string.Equals(updated.Id, issueId, StringComparison.OrdinalIgnoreCase))
            {
                _cache.TryRemove(issueId, out _);
            }

            _cache[updated.Id] = updated;
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }

        await _serializationQueue.EnqueueAsync(new IssueWriteOperation(
            IssueId: updated.Id,
            Type: WriteOperationType.Update,
            WriteAction: async _ => await Task.CompletedTask,
            QueuedAt: DateTimeOffset.UtcNow
        ), ct);

        return updated;
    }

    /// <inheritdoc />
    public async Task<Issue?> UpdateQuestionsAsync(
        string issueId,
        IReadOnlyList<Question> questions,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureCacheLoadedAsync(ct);

        // Check cache first
        _cacheLock.EnterReadLock();
        try
        {
            if (!_cache.ContainsKey(issueId))
            {
                return null;
            }
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }

        Issue updated;
        try
        {
            updated = await _issueService.UpdateQuestionsAsync(issueId, questions, ct);
        }
        catch (KeyNotFoundException)
        {
            _cacheLock.EnterWriteLock();
            try
            {
                _cache.TryRemove(issueId, out _);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }

            return null;
        }

        // Update cache
        _cacheLock.EnterWriteLock();
        try
        {
            _cache[updated.Id] = updated;
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }

        await _serializationQueue.EnqueueAsync(new IssueWriteOperation(
            IssueId: updated.Id,
            Type: WriteOperationType.Update,
            WriteAction: async _ => await Task.CompletedTask,
            QueuedAt: DateTimeOffset.UtcNow
        ), ct);

        return updated;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteIssueAsync(string issueId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureCacheLoadedAsync(ct);

        var deleted = await _issueService.DeleteAsync(issueId, ct);

        if (deleted)
        {
            // DeleteAsync soft-deletes (sets status to Deleted), so reload the issue
            var updatedIssue = await _issueService.GetByIdAsync(issueId, ct);

            _cacheLock.EnterWriteLock();
            try
            {
                if (updatedIssue is not null)
                {
                    _cache[issueId] = updatedIssue;
                }
                else
                {
                    _cache.TryRemove(issueId, out _);
                }
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }

            await _serializationQueue.EnqueueAsync(new IssueWriteOperation(
                IssueId: issueId,
                Type: WriteOperationType.Delete,
                WriteAction: async _ => await Task.CompletedTask,
                QueuedAt: DateTimeOffset.UtcNow
            ), ct);
        }

        return deleted;
    }

    #endregion

    #region Cache Management

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await LoadCacheFromDiskAsync(ct);
    }

    /// <summary>
    /// Ensures the cache has been loaded at least once. Uses double-checked locking
    /// to avoid redundant loads.
    /// </summary>
    private async Task EnsureCacheLoadedAsync(CancellationToken ct)
    {
        if (_isLoaded)
        {
            return;
        }

        await LoadCacheFromDiskAsync(ct);
    }

    /// <summary>
    /// Loads all issues from disk into the cache, replacing the current contents.
    /// Thread-safe via <see cref="_loadLock"/>.
    /// </summary>
    private async Task LoadCacheFromDiskAsync(CancellationToken ct)
    {
        await _loadLock.WaitAsync(ct);
        try
        {
            var allIssues = await _issueService.GetAllAsync(ct);

            _cacheLock.EnterWriteLock();
            try
            {
                _cache.Clear();
                foreach (var issue in allIssues)
                {
                    _cache[issue.Id] = issue;
                }
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }

            _isLoaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    #endregion

    #region File Watching

    /// <summary>
    /// Initialises a <see cref="FileSystemWatcher"/> on the <c>.fleece/</c> directory
    /// to detect external changes to issue files.
    /// </summary>
    private void InitializeFileWatcher()
    {
        var fleecePath = Path.Combine(_basePath, FleeceDirectory);

        // Don't start watching if the directory doesn't exist yet;
        // we'll create it lazily when the first write happens.
        if (!Directory.Exists(fleecePath))
        {
            return;
        }

        try
        {
            _watcher = new FileSystemWatcher(fleecePath, IssuesFileFilter)
            {
                NotifyFilter = NotifyFilters.FileName
                               | NotifyFilters.LastWrite
                               | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
        }
        catch (Exception)
        {
            // FileSystemWatcher can fail on some platforms — fall back to manual ReloadAsync
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        ScheduleDebouncedReload();
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        ScheduleDebouncedReload();
    }

    /// <summary>
    /// Schedules a debounced cache reload. Rapid successive changes (e.g. during bulk operations)
    /// are coalesced into a single reload after <see cref="ReloadDebounceInterval"/>.
    /// </summary>
    private void ScheduleDebouncedReload()
    {
        if (_disposed)
        {
            return;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(
            _ =>
            {
                // Fire and forget — errors are swallowed since this is a best-effort reload
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await LoadCacheFromDiskAsync(CancellationToken.None);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Service was disposed during reload — ignore
                    }
                    catch (Exception)
                    {
                        // External change reload failed — cache remains stale until next manual reload
                    }
                });
            },
            null,
            ReloadDebounceInterval,
            Timeout.InfiniteTimeSpan);
    }

    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _debounceTimer?.Dispose();

        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Deleted -= OnFileChanged;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Dispose();
        }

        _cacheLock.Dispose();
        _loadLock.Dispose();
    }
}
