using System.IO.Abstractions;
using System.Text.Json;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.FunctionalCore.Legacy;
using Fleece.Core.Models;
using Fleece.Core.Models.Legacy;
using Fleece.Core.Serialization.Legacy;

namespace Fleece.Core.EventSourcing.Services.Legacy;

/// <summary>
/// Default <see cref="IEventMigrationService"/>. Reads legacy hashed files using
/// the <see cref="LegacyIssue"/> shape, runs <see cref="LegacyMerging"/> to produce
/// a single consolidated set, projects each <see cref="LegacyIssue"/> into the
/// lean <see cref="Issue"/> shape, writes the new snapshot/tombstones, deletes the
/// legacy files, ensures <c>.fleece/changes/</c> exists, and adds the runtime
/// gitignore entries.
/// </summary>
public sealed class EventMigrationService : IEventMigrationService
{
    private const string FleeceDirectory = ".fleece";
    private const string ChangesDirectory = "changes";
    private const string IssuesSnapshotFileName = "issues.jsonl";
    private const string TombstonesSnapshotFileName = "tombstones.jsonl";
    private const string LegacyIssuesPattern = "issues_*.jsonl";
    private const string LegacyTombstonesPattern = "tombstones_*.jsonl";

    private static readonly string[] GitignoreEntries =
    [
        ".fleece/.active-change",
        ".fleece/.replay-cache",
    ];

    private readonly string _basePath;
    private readonly IFileSystem _fileSystem;

    public EventMigrationService(string basePath, IFileSystem? fileSystem = null)
    {
        _basePath = basePath;
        _fileSystem = fileSystem ?? new Testably.Abstractions.RealFileSystem();
    }

    private string FleeceDirectoryPath => _fileSystem.Path.Combine(_basePath, FleeceDirectory);
    private string ChangesDirectoryPath => _fileSystem.Path.Combine(FleeceDirectoryPath, ChangesDirectory);
    private string SnapshotPath => _fileSystem.Path.Combine(FleeceDirectoryPath, IssuesSnapshotFileName);
    private string TombstonesPath => _fileSystem.Path.Combine(FleeceDirectoryPath, TombstonesSnapshotFileName);
    private string GitignorePath => _fileSystem.Path.Combine(_basePath, ".gitignore");

    public Task<bool> IsMigrationNeededAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.Directory.Exists(FleeceDirectoryPath))
        {
            return Task.FromResult(false);
        }
        var legacyIssues = _fileSystem.Directory.GetFiles(FleeceDirectoryPath, LegacyIssuesPattern);
        var legacyTombs = _fileSystem.Directory.GetFiles(FleeceDirectoryPath, LegacyTombstonesPattern);
        return Task.FromResult(legacyIssues.Length > 0 || legacyTombs.Length > 0);
    }

    public async Task<EventMigrationResult> MigrateAsync(
        string? mergedBy = null,
        CancellationToken cancellationToken = default)
    {
        if (!await IsMigrationNeededAsync(cancellationToken))
        {
            return new EventMigrationResult
            {
                WasMigrationNeeded = false,
                LegacyIssueFilesConsumed = 0,
                LegacyTombstoneFilesConsumed = 0,
                IssuesWritten = 0,
                TombstonesWritten = 0,
                GitignoreEntriesAdded = [],
            };
        }

        _fileSystem.Directory.CreateDirectory(FleeceDirectoryPath);

        var legacyIssueFiles = _fileSystem.Directory.GetFiles(FleeceDirectoryPath, LegacyIssuesPattern);
        Array.Sort(legacyIssueFiles, StringComparer.Ordinal);
        var legacyTombFiles = _fileSystem.Directory.GetFiles(FleeceDirectoryPath, LegacyTombstonesPattern);
        Array.Sort(legacyTombFiles, StringComparer.Ordinal);

        // 1. Read legacy issues per file.
        var fileGroups = new List<(string, IReadOnlyList<LegacyIssue>)>();
        foreach (var path in legacyIssueFiles)
        {
            var issues = await ReadLegacyIssuesAsync(path, cancellationToken);
            fileGroups.Add((path, issues));
        }

        // 2. Merge cross-file duplicates with the legacy property-level merger.
        var plan = LegacyMerging.Plan(fileGroups, mergedBy);
        var consolidatedLegacy = LegacyMerging.Apply(plan);

        // 3. Project legacy → lean Issue.
        var leanIssues = consolidatedLegacy.Select(ToLeanIssue).ToList();

        // 4. Read & union tombstones.
        var tombstones = new Dictionary<string, Tombstone>(StringComparer.Ordinal);
        foreach (var path in legacyTombFiles)
        {
            foreach (var t in await ReadLegacyTombstonesAsync(path, cancellationToken))
            {
                if (!tombstones.ContainsKey(t.IssueId))
                {
                    tombstones[t.IssueId] = t;
                }
            }
        }

        // 5. Write the new snapshot & tombstones.
        await WriteSnapshotAsync(leanIssues, cancellationToken);
        await WriteTombstonesAsync(tombstones.Values.OrderBy(t => t.IssueId, StringComparer.Ordinal).ToList(), cancellationToken);

        // 6. Delete legacy files.
        foreach (var path in legacyIssueFiles)
        {
            _fileSystem.File.Delete(path);
        }
        foreach (var path in legacyTombFiles)
        {
            _fileSystem.File.Delete(path);
        }

        // 7. Ensure changes directory exists.
        if (!_fileSystem.Directory.Exists(ChangesDirectoryPath))
        {
            _fileSystem.Directory.CreateDirectory(ChangesDirectoryPath);
        }

        // 8. Add gitignore entries.
        var gitignoreAdded = await EnsureGitignoreEntriesAsync(cancellationToken);

        return new EventMigrationResult
        {
            WasMigrationNeeded = true,
            LegacyIssueFilesConsumed = legacyIssueFiles.Length,
            LegacyTombstoneFilesConsumed = legacyTombFiles.Length,
            IssuesWritten = leanIssues.Count,
            TombstonesWritten = tombstones.Count,
            GitignoreEntriesAdded = gitignoreAdded,
        };
    }

    private async Task<IReadOnlyList<LegacyIssue>> ReadLegacyIssuesAsync(string path, CancellationToken cancellationToken)
    {
        var content = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken);
        var result = new List<LegacyIssue>();
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            var issue = JsonSerializer.Deserialize(line, FleeceLegacyJsonContext.Default.LegacyIssue);
            if (issue is not null)
            {
                result.Add(issue);
            }
        }
        return result;
    }

    private async Task<IReadOnlyList<Tombstone>> ReadLegacyTombstonesAsync(string path, CancellationToken cancellationToken)
    {
        var content = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken);
        var result = new List<Tombstone>();
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            var t = JsonSerializer.Deserialize(line, FleeceLegacyJsonContext.Default.Tombstone);
            if (t is not null)
            {
                result.Add(t);
            }
        }
        return result;
    }

    private async Task WriteSnapshotAsync(IReadOnlyList<Issue> issues, CancellationToken cancellationToken)
    {
        var sorted = issues.OrderBy(i => i.Id, StringComparer.Ordinal);
        var lines = sorted.Select(i => JsonSerializer.Serialize(i, EventSourcingJsonContext.Default.Issue)).ToList();
        var body = lines.Count == 0 ? string.Empty : string.Join('\n', lines) + "\n";
        await _fileSystem.File.WriteAllTextAsync(SnapshotPath, body, cancellationToken);
    }

    private async Task WriteTombstonesAsync(IReadOnlyList<Tombstone> tombstones, CancellationToken cancellationToken)
    {
        var lines = tombstones.Select(t => JsonSerializer.Serialize(t, EventSourcingJsonContext.Default.Tombstone)).ToList();
        var body = lines.Count == 0 ? string.Empty : string.Join('\n', lines) + "\n";
        await _fileSystem.File.WriteAllTextAsync(TombstonesPath, body, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> EnsureGitignoreEntriesAsync(CancellationToken cancellationToken)
    {
        var existing = _fileSystem.File.Exists(GitignorePath)
            ? await _fileSystem.File.ReadAllTextAsync(GitignorePath, cancellationToken)
            : string.Empty;

        var lines = existing.Split('\n').Select(l => l.TrimEnd('\r').Trim()).ToList();
        var added = new List<string>();
        foreach (var entry in GitignoreEntries)
        {
            if (!lines.Any(l => string.Equals(l, entry, StringComparison.Ordinal)))
            {
                added.Add(entry);
            }
        }
        if (added.Count == 0)
        {
            return [];
        }

        var sb = new System.Text.StringBuilder();
        sb.Append(existing.TrimEnd('\n'));
        if (sb.Length > 0)
        {
            sb.Append('\n');
        }
        foreach (var entry in added)
        {
            sb.Append(entry);
            sb.Append('\n');
        }
        await _fileSystem.File.WriteAllTextAsync(GitignorePath, sb.ToString(), cancellationToken);
        return added;
    }

    private static Issue ToLeanIssue(LegacyIssue legacy) => new()
    {
        Id = legacy.Id,
        Title = legacy.Title,
        Description = legacy.Description,
        Status = legacy.Status,
        Type = legacy.Type,
        LinkedPR = legacy.LinkedPR,
        LinkedIssues = legacy.LinkedIssues ?? [],
        ParentIssues = (legacy.ParentIssues ?? [])
            .Select(p => new ParentIssueRef
            {
                ParentIssue = p.ParentIssue,
                SortOrder = p.SortOrder,
                Active = p.Active,
            })
            .ToList(),
        Priority = legacy.Priority,
        AssignedTo = legacy.AssignedTo,
        Tags = legacy.Tags ?? [],
        WorkingBranchId = legacy.WorkingBranchId,
        ExecutionMode = legacy.ExecutionMode,
        CreatedBy = legacy.CreatedBy,
        CreatedAt = legacy.CreatedAt,
        LastUpdate = legacy.LastUpdate,
    };
}
