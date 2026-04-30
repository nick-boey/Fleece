using System.IO.Abstractions;
using System.Text.Json;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services;

public sealed class SnapshotStore : ISnapshotStore
{
    internal const string FleeceDirectory = ".fleece";
    internal const string SnapshotFileName = "issues.jsonl";
    internal const string TombstonesFileName = "tombstones.jsonl";

    private readonly string _basePath;
    private readonly IFileSystem _fileSystem;

    public SnapshotStore(string basePath, IFileSystem? fileSystem = null)
    {
        _basePath = basePath;
        _fileSystem = fileSystem ?? new Testably.Abstractions.RealFileSystem();
    }

    public string SnapshotPath => _fileSystem.Path.Combine(_basePath, FleeceDirectory, SnapshotFileName);
    public string TombstonesPath => _fileSystem.Path.Combine(_basePath, FleeceDirectory, TombstonesFileName);

    public async Task<IReadOnlyDictionary<string, Issue>> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.File.Exists(SnapshotPath))
        {
            return new Dictionary<string, Issue>(StringComparer.Ordinal);
        }
        var content = await _fileSystem.File.ReadAllTextAsync(SnapshotPath, cancellationToken);
        var dict = new Dictionary<string, Issue>(StringComparer.Ordinal);
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            var issue = JsonSerializer.Deserialize(line, EventSourcingJsonContext.Default.Issue);
            if (issue is not null)
            {
                dict[issue.Id] = issue;
            }
        }
        return dict;
    }

    public async Task WriteSnapshotAsync(IReadOnlyDictionary<string, Issue> issues, CancellationToken cancellationToken = default)
    {
        EnsureDirectory();
        var sorted = issues.Values.OrderBy(i => i.Id, StringComparer.Ordinal);
        var lines = sorted.Select(i => JsonSerializer.Serialize(i, EventSourcingJsonContext.Default.Issue));
        await _fileSystem.File.WriteAllTextAsync(SnapshotPath, JoinJsonl(lines), cancellationToken);
    }

    public async Task<IReadOnlyList<Tombstone>> LoadTombstonesAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.File.Exists(TombstonesPath))
        {
            return [];
        }
        var content = await _fileSystem.File.ReadAllTextAsync(TombstonesPath, cancellationToken);
        var list = new List<Tombstone>();
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            var ts = JsonSerializer.Deserialize(line, EventSourcingJsonContext.Default.Tombstone);
            if (ts is not null)
            {
                list.Add(ts);
            }
        }
        return list;
    }

    public async Task WriteTombstonesAsync(IReadOnlyList<Tombstone> tombstones, CancellationToken cancellationToken = default)
    {
        EnsureDirectory();
        var sorted = tombstones.OrderBy(t => t.IssueId, StringComparer.Ordinal);
        var lines = sorted.Select(t => JsonSerializer.Serialize(t, EventSourcingJsonContext.Default.Tombstone));
        await _fileSystem.File.WriteAllTextAsync(TombstonesPath, JoinJsonl(lines), cancellationToken);
    }

    private void EnsureDirectory()
    {
        var dir = _fileSystem.Path.Combine(_basePath, FleeceDirectory);
        if (!_fileSystem.Directory.Exists(dir))
        {
            _fileSystem.Directory.CreateDirectory(dir);
        }
    }

    private static string JoinJsonl(IEnumerable<string> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0)
        {
            return string.Empty;
        }
        return string.Join('\n', list) + "\n";
    }
}
