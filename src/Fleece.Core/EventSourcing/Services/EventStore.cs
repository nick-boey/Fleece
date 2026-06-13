using System.IO.Abstractions;
using System.Text;
using Fleece.Core.EventSourcing.Events;
using Fleece.Core.EventSourcing.Services.Interfaces;

namespace Fleece.Core.EventSourcing.Services;

/// <summary>
/// Default <see cref="IEventStore"/> implementation. Persists each issue as a single
/// append-only event log at <c>.fleece/issues/{id}.jsonl</c>. Uses <see cref="IFileSystem"/>
/// for all I/O so tests can substitute an in-memory file system.
/// </summary>
public sealed class EventStore : IEventStore
{
    internal const string FleeceDirectory = ".fleece";
    internal const string IssuesDirectory = "issues";
    private const string LogFileExtension = ".jsonl";

    private readonly string _basePath;
    private readonly IFileSystem _fileSystem;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public EventStore(
        string basePath,
        IFileSystem? fileSystem = null)
    {
        _basePath = basePath;
        _fileSystem = fileSystem ?? new Testably.Abstractions.RealFileSystem();
    }

    private string FleeceDirectoryPath => _fileSystem.Path.Combine(_basePath, FleeceDirectory);
    private string IssuesDirectoryPath => _fileSystem.Path.Combine(FleeceDirectoryPath, IssuesDirectory);

    public Task<IReadOnlyList<string>> GetAllIssueLogPathsAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.Directory.Exists(IssuesDirectoryPath))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
        var files = _fileSystem.Directory.GetFiles(IssuesDirectoryPath, $"*{LogFileExtension}");
        Array.Sort(files, StringComparer.Ordinal);
        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public async Task<IReadOnlyList<IssueEvent>> ReadIssueLogAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken);
        var events = new List<IssueEvent>();
        var lineNumber = 0;
        foreach (var rawLine in content.Split('\n'))
        {
            lineNumber++;
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            events.Add(EventJsonSerializer.ParseLine(line, filePath, lineNumber));
        }
        return events;
    }

    public async Task AppendEventsAsync(IReadOnlyList<IssueEvent> events, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            throw new ArgumentException("AppendEventsAsync requires at least one event.", nameof(events));
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // Group by issue id, preserving append order within each group, and write each
            // group to that issue's own log file. File order within a single issue is truth.
            foreach (var group in GroupByIssue(events))
            {
                var path = LogFilePathFor(group.Key);
                await AppendLinesAsync(path, group.Value.Select(EventJsonSerializer.Serialize), cancellationToken);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteIssueLogAsync(string issueId, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var path = LogFilePathFor(issueId);
            if (_fileSystem.File.Exists(path))
            {
                _fileSystem.File.Delete(path);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static IReadOnlyList<KeyValuePair<string, List<IssueEvent>>> GroupByIssue(IReadOnlyList<IssueEvent> events)
    {
        var order = new List<string>();
        var groups = new Dictionary<string, List<IssueEvent>>(StringComparer.Ordinal);
        foreach (var evt in events)
        {
            var id = IssueIdOf(evt);
            if (!groups.TryGetValue(id, out var list))
            {
                list = [];
                groups[id] = list;
                order.Add(id);
            }
            list.Add(evt);
        }
        return order.Select(id => new KeyValuePair<string, List<IssueEvent>>(id, groups[id])).ToList();
    }

    private static string IssueIdOf(IssueEvent evt) => evt switch
    {
        CreateEvent c => c.IssueId,
        SetEvent s => s.IssueId,
        AddEvent a => a.IssueId,
        RemoveEvent r => r.IssueId,
        _ => throw new InvalidOperationException($"Event of kind '{evt.Kind}' has no issue id."),
    };

    private async Task AppendLinesAsync(string filePath, IEnumerable<string> lines, CancellationToken cancellationToken)
    {
        EnsureIssuesDirectory();

        // Open with FileShare.Read so concurrent reads are allowed.
        // FileMode.Append creates if missing, otherwise positions at end.
        await using var stream = _fileSystem.File.Open(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        foreach (var line in lines)
        {
            await writer.WriteAsync(line.AsMemory(), cancellationToken);
            await writer.WriteAsync('\n');
        }
        await writer.FlushAsync(cancellationToken);
        // Best-effort durability flush; not all IFileSystem implementations expose fsync.
        try
        {
            await stream.FlushAsync(cancellationToken);
        }
        catch
        {
            // Mock filesystems may not implement FlushAsync; ignore.
        }
    }

    private void EnsureIssuesDirectory()
    {
        if (!_fileSystem.Directory.Exists(FleeceDirectoryPath))
        {
            _fileSystem.Directory.CreateDirectory(FleeceDirectoryPath);
        }
        if (!_fileSystem.Directory.Exists(IssuesDirectoryPath))
        {
            _fileSystem.Directory.CreateDirectory(IssuesDirectoryPath);
        }
    }

    private string LogFilePathFor(string issueId) =>
        _fileSystem.Path.Combine(IssuesDirectoryPath, $"{issueId}{LogFileExtension}");
}
