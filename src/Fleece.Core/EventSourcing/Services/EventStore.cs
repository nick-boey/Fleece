using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using Fleece.Core.EventSourcing.Events;
using Fleece.Core.EventSourcing.Services.Interfaces;

namespace Fleece.Core.EventSourcing.Services;

/// <summary>
/// Default <see cref="IEventStore"/> implementation. Uses <see cref="IFileSystem"/> for
/// all I/O so tests can substitute an in-memory file system.
/// </summary>
public sealed class EventStore : IEventStore
{
    internal const string FleeceDirectory = ".fleece";
    internal const string ChangesDirectory = "changes";
    internal const string ActiveChangeFileName = ".active-change";
    private const string ChangeFilePrefix = "change_";
    private const string ChangeFileExtension = ".jsonl";

    private readonly string _basePath;
    private readonly IFileSystem _fileSystem;
    private readonly Func<string> _guidFactory;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public EventStore(string basePath, IFileSystem? fileSystem = null, Func<string>? guidFactory = null)
    {
        _basePath = basePath;
        _fileSystem = fileSystem ?? new Testably.Abstractions.RealFileSystem();
        _guidFactory = guidFactory ?? (() => Guid.NewGuid().ToString("N"));
    }

    private string FleeceDirectoryPath => _fileSystem.Path.Combine(_basePath, FleeceDirectory);
    private string ChangesDirectoryPath => _fileSystem.Path.Combine(FleeceDirectoryPath, ChangesDirectory);
    private string ActiveChangeFilePath => _fileSystem.Path.Combine(FleeceDirectoryPath, ActiveChangeFileName);

    public Task<IReadOnlyList<string>> GetAllChangeFilePathsAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.Directory.Exists(ChangesDirectoryPath))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
        var files = _fileSystem.Directory.GetFiles(ChangesDirectoryPath, $"{ChangeFilePrefix}*{ChangeFileExtension}");
        Array.Sort(files, StringComparer.Ordinal);
        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public async Task<IReadOnlyList<IssueEvent>> ReadChangeFileAsync(string filePath, CancellationToken cancellationToken = default)
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
            var evt = EventJsonSerializer.ParseLine(line, filePath, lineNumber);
            if (events.Count == 0 && evt is not MetaEvent)
            {
                throw new InvalidOperationException(
                    $"Change file {filePath} does not begin with a meta event (line 1 was '{evt.Kind}').");
            }
            events.Add(evt);
        }
        if (events.Count == 0)
        {
            throw new InvalidOperationException($"Change file {filePath} is empty.");
        }
        return events;
    }

    public async Task<MetaEvent> ReadMetaAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Read just the first non-blank line for efficiency on large change files.
        await using var stream = _fileSystem.File.OpenRead(filePath);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var lineNumber = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            var evt = EventJsonSerializer.ParseLine(line, filePath, lineNumber);
            if (evt is MetaEvent meta)
            {
                return meta;
            }
            throw new InvalidOperationException(
                $"Change file {filePath} does not begin with a meta event (line {lineNumber} was '{evt.Kind}').");
        }
        throw new InvalidOperationException($"Change file {filePath} is empty.");
    }

    public async Task<string?> GetActiveChangeFilePathAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.File.Exists(ActiveChangeFilePath))
        {
            return null;
        }
        var content = await _fileSystem.File.ReadAllTextAsync(ActiveChangeFilePath, cancellationToken);
        var pointer = JsonSerializer.Deserialize(content, EventSourcingJsonContext.Default.ActiveChangePointer);
        if (pointer is null || string.IsNullOrEmpty(pointer.Guid))
        {
            return null;
        }
        return ChangeFilePathFor(pointer.Guid);
    }

    public async Task<string> AppendEventsAsync(IReadOnlyList<IssueEvent> events, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            throw new ArgumentException("AppendEventsAsync requires at least one event.", nameof(events));
        }
        if (events.Any(e => e is MetaEvent))
        {
            throw new ArgumentException(
                "Meta events are written automatically on rotation and cannot be passed to AppendEventsAsync.",
                nameof(events));
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var activePath = await ResolveActiveOrRotateAsync(cancellationToken);
            await AppendLinesAsync(activePath, events.Select(EventJsonSerializer.Serialize), cancellationToken);
            return activePath;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<string> RotateAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            return await RotateInternalAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteAllChangeFilesAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (_fileSystem.Directory.Exists(ChangesDirectoryPath))
            {
                foreach (var path in _fileSystem.Directory.GetFiles(ChangesDirectoryPath, $"{ChangeFilePrefix}*{ChangeFileExtension}"))
                {
                    _fileSystem.File.Delete(path);
                }
            }
            if (_fileSystem.File.Exists(ActiveChangeFilePath))
            {
                _fileSystem.File.Delete(ActiveChangeFilePath);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Returns the active change file path. Triggers a rotation if the pointer is missing
    /// or the file it references no longer exists on disk. Caller MUST hold <see cref="_writeLock"/>.
    /// </summary>
    private async Task<string> ResolveActiveOrRotateAsync(CancellationToken cancellationToken)
    {
        var activePath = await GetActiveChangeFilePathAsync(cancellationToken);
        if (activePath is not null && _fileSystem.File.Exists(activePath))
        {
            return activePath;
        }
        return await RotateInternalAsync(cancellationToken);
    }

    private async Task<string> RotateInternalAsync(CancellationToken cancellationToken)
    {
        EnsureChangesDirectory();

        var leaf = await FindDagLeafAsync(cancellationToken);
        var newGuid = _guidFactory();
        var newPath = ChangeFilePathFor(newGuid);

        // Avoid overwriting an existing file (extremely unlikely with full GUIDs).
        if (_fileSystem.File.Exists(newPath))
        {
            throw new InvalidOperationException(
                $"Generated GUID '{newGuid}' collides with existing change file at {newPath}.");
        }

        var meta = new MetaEvent { Follows = leaf };
        await AppendLinesAsync(newPath, [EventJsonSerializer.Serialize(meta)], cancellationToken);

        var pointer = new ActiveChangePointer { Guid = newGuid };
        var pointerJson = JsonSerializer.Serialize(pointer, EventSourcingJsonContext.Default.ActiveChangePointer);
        await _fileSystem.File.WriteAllTextAsync(ActiveChangeFilePath, pointerJson + "\n", cancellationToken);

        return newPath;
    }

    private async Task<string?> FindDagLeafAsync(CancellationToken cancellationToken)
    {
        var allPaths = await GetAllChangeFilePathsAsync(cancellationToken);
        if (allPaths.Count == 0)
        {
            return null;
        }

        var guids = allPaths.Select(ExtractGuidFromPath).ToList();
        var guidSet = guids.ToHashSet(StringComparer.Ordinal);
        var followsByGuid = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in allPaths)
        {
            var meta = await ReadMetaAsync(path, cancellationToken);
            followsByGuid[ExtractGuidFromPath(path)] = meta.Follows;
        }

        // A leaf is a GUID that no other file's `follows` references.
        var hasDescendant = followsByGuid.Values
            .Where(f => f is not null && guidSet.Contains(f!))
            .ToHashSet(StringComparer.Ordinal)!;

        var leaves = guids.Where(g => !hasDescendant.Contains(g)).ToList();
        if (leaves.Count == 0)
        {
            // Cycle in the DAG (shouldn't happen with honest rotation but be defensive).
            // Fall back to alphabetical first.
            leaves = [.. guids];
        }
        leaves.Sort(StringComparer.Ordinal);
        return leaves[0];
    }

    private async Task AppendLinesAsync(string filePath, IEnumerable<string> lines, CancellationToken cancellationToken)
    {
        EnsureChangesDirectory();

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

    private void EnsureChangesDirectory()
    {
        if (!_fileSystem.Directory.Exists(FleeceDirectoryPath))
        {
            _fileSystem.Directory.CreateDirectory(FleeceDirectoryPath);
        }
        if (!_fileSystem.Directory.Exists(ChangesDirectoryPath))
        {
            _fileSystem.Directory.CreateDirectory(ChangesDirectoryPath);
        }
    }

    private string ChangeFilePathFor(string guid) =>
        _fileSystem.Path.Combine(ChangesDirectoryPath, $"{ChangeFilePrefix}{guid}{ChangeFileExtension}");

    /// <summary>
    /// Extracts the GUID portion of <c>change_{guid}.jsonl</c>. Internal so the replay
    /// engine can do the same lookup.
    /// </summary>
    internal static string ExtractGuidFromPath(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        if (!name.StartsWith(ChangeFilePrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Path does not match the change_{{guid}}.jsonl pattern: {filePath}");
        }
        return name[ChangeFilePrefix.Length..];
    }
}
