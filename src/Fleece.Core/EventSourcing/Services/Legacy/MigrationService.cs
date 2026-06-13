using System.IO.Abstractions;
using System.Text.Json;
using Fleece.Core.EventSourcing.Events;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.FunctionalCore.Legacy;
using Fleece.Core.Models;
using Fleece.Core.Models.Legacy;
using Fleece.Core.Serialization.Legacy;

namespace Fleece.Core.EventSourcing.Services.Legacy;

/// <summary>
/// Default <see cref="IMigrationService"/>. The explicit <c>fleece migrate</c> command converts
/// whichever legacy layout it finds into the v4 event-sourced layout (one append-only per-issue
/// log at <c>.fleece/issues/&lt;id&gt;.jsonl</c>):
/// <list type="bullet">
/// <item>Layout A — hashed files (<c>issues_*.jsonl</c> + <c>tombstones_*.jsonl</c>): read using
/// the <see cref="LegacyIssue"/> shape, fixed up via <see cref="LegacyMigration.Migrate"/>,
/// consolidated by <see cref="LegacyMerging"/>, projected to the lean <see cref="Issue"/> shape.</item>
/// <item>Layout B — the durable snapshot (<c>.fleece/issues.jsonl</c> + <c>.fleece/changes/</c>):
/// replayed read-only via <see cref="DurableSnapshotReader"/> to each issue's current state.</item>
/// </list>
/// Either way, one <c>create</c> event is written per issue and the consumed sources are deleted.
/// v4 keeps no tombstone sidecar, no <c>.fleece/changes/</c> directory, and no gitignored
/// pointer/cache files, so none are written. Only Layout A is auto-migrated by the interceptor
/// (see <see cref="IsMigrationNeededAsync"/>); Layout B converts only on an explicit
/// <c>fleece migrate</c>.
/// </summary>
public sealed class MigrationService : IMigrationService
{
    private const string FleeceDirectory = ".fleece";
    private const string LegacyIssuesPattern = "issues_*.jsonl";
    private const string LegacyTombstonesPattern = "tombstones_*.jsonl";

    private readonly string _basePath;
    private readonly IFileSystem _fileSystem;
    private readonly IEventStore _eventStore;
    private readonly DurableSnapshotReader _durableReader;

    public MigrationService(string basePath, IFileSystem? fileSystem = null)
    {
        _basePath = basePath;
        _fileSystem = fileSystem ?? new Testably.Abstractions.RealFileSystem();
        _eventStore = new EventStore(basePath, _fileSystem);
        _durableReader = new DurableSnapshotReader(basePath, _fileSystem);
    }

    private string FleeceDirectoryPath => _fileSystem.Path.Combine(_basePath, FleeceDirectory);

    public Task<bool> IsMigrationNeededAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.Directory.Exists(FleeceDirectoryPath))
        {
            return Task.FromResult(false);
        }

        // This is the interceptor's auto-trigger, so it reports ONLY the hashed-file layout
        // (Layout A). A legacy durable `.fleece/issues.jsonl` snapshot (Layout B) is NOT
        // auto-migrated — it triggers a v4-migration warning instead (see
        // AutoMigrateInterceptor) and is converted only by the explicit `fleece migrate`
        // command (MigrateAsync detects it separately).
        var legacyIssues = _fileSystem.Directory.GetFiles(FleeceDirectoryPath, LegacyIssuesPattern);
        var legacyTombs = _fileSystem.Directory.GetFiles(FleeceDirectoryPath, LegacyTombstonesPattern);
        return Task.FromResult(legacyIssues.Length > 0 || legacyTombs.Length > 0);
    }

    public async Task<MigrationResult> MigrateAsync(
        string? mergedBy = null,
        bool convertDurableLayout = true,
        CancellationToken cancellationToken = default)
    {
        // The explicit `fleece migrate` command converts whichever legacy layout is present:
        //   - Layout A: hashed files (`issues_*.jsonl` + `tombstones_*.jsonl`), gated by
        //     IsMigrationNeededAsync (also the interceptor's auto-trigger).
        //   - Layout B: the durable snapshot (`.fleece/issues.jsonl` + `.fleece/changes/`),
        //     detected here only — never auto-converted by the interceptor.
        // The interceptor's auto-migration passes convertDurableLayout=false so it never touches
        // the durable layout, even in a repository that ALSO has hashed files.
        var hashedNeeded = await IsMigrationNeededAsync(cancellationToken);
        var durablePresent = convertDurableLayout && _durableReader.IsDurableLayoutPresent();

        if (!hashedNeeded && !durablePresent)
        {
            return new MigrationResult
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

        var legacyIssueFilesConsumed = 0;
        var legacyTombFilesConsumed = 0;
        var issuesWritten = 0;

        if (hashedNeeded)
        {
            (legacyIssueFilesConsumed, legacyTombFilesConsumed, var hashedIssuesWritten) =
                await ConvertHashedLayoutAsync(mergedBy, cancellationToken);
            issuesWritten += hashedIssuesWritten;
        }

        if (durablePresent)
        {
            issuesWritten += await ConvertDurableLayoutAsync(mergedBy, cancellationToken);
        }

        return new MigrationResult
        {
            WasMigrationNeeded = true,
            LegacyIssueFilesConsumed = legacyIssueFilesConsumed,
            LegacyTombstoneFilesConsumed = legacyTombFilesConsumed,
            IssuesWritten = issuesWritten,
            TombstonesWritten = 0,
            GitignoreEntriesAdded = [],
        };
    }

    /// <summary>
    /// Converts the legacy hashed-file layout (Layout A). Returns the number of issue files and
    /// tombstone files consumed and the count of per-issue logs written.
    /// </summary>
    private async Task<(int IssueFiles, int TombstoneFiles, int IssuesWritten)> ConvertHashedLayoutAsync(
        string? mergedBy, CancellationToken cancellationToken)
    {
        var legacyIssueFiles = _fileSystem.Directory.GetFiles(FleeceDirectoryPath, LegacyIssuesPattern);
        Array.Sort(legacyIssueFiles, StringComparer.Ordinal);
        var legacyTombFiles = _fileSystem.Directory.GetFiles(FleeceDirectoryPath, LegacyTombstonesPattern);
        Array.Sort(legacyTombFiles, StringComparer.Ordinal);

        // 1. Read legacy issues per file, applying pre-3.0.0 intra-shape fixups
        //    (timestamp backfill, LinkedPR → Tags fold, parent-ref LastUpdated backfill,
        //    unknown-property strip) before any cross-file merging.
        var fileGroups = new List<(string, IReadOnlyList<LegacyIssue>)>();
        foreach (var path in legacyIssueFiles)
        {
            var issues = await ReadLegacyIssuesAsync(path, cancellationToken);
            var fixedUp = LegacyMigration.Migrate(issues);
            fileGroups.Add((path, fixedUp));
        }

        // 2. Merge cross-file duplicates with the legacy property-level merger.
        var plan = LegacyMerging.Plan(fileGroups, mergedBy);
        var consolidatedLegacy = LegacyMerging.Apply(plan);

        // 3. Project legacy → lean Issue.
        var leanIssues = consolidatedLegacy.Select(ToLeanIssue).ToList();

        // 4. Write one append-only per-issue log per issue (v4 layout). A single `create`
        //    event captures the issue's whole state; later edits append on the same branch.
        if (leanIssues.Count > 0)
        {
            var events = leanIssues
                .OrderBy(i => i.Id, StringComparer.Ordinal)
                .Select(i => BuildCreateEvent(i, mergedBy))
                .Cast<IssueEvent>()
                .ToList();
            await _eventStore.AppendEventsAsync(events, cancellationToken);
        }

        // 5. Delete legacy files. v4 keeps no tombstone sidecar, so tombstone files are simply
        //    consumed (the issues they referenced were already absent from the live set).
        foreach (var path in legacyIssueFiles)
        {
            _fileSystem.File.Delete(path);
        }
        foreach (var path in legacyTombFiles)
        {
            _fileSystem.File.Delete(path);
        }

        return (legacyIssueFiles.Length, legacyTombFiles.Length, leanIssues.Count);
    }

    /// <summary>
    /// Converts the legacy durable snapshot layout (Layout B). Replays the snapshot + change
    /// files to each issue's current state, writes one <c>create</c> event per issue, then
    /// deletes the consumed snapshot and <c>.fleece/changes/</c> directory. Returns the count of
    /// per-issue logs written.
    /// </summary>
    private async Task<int> ConvertDurableLayoutAsync(string? mergedBy, CancellationToken cancellationToken)
    {
        var durableIssues = await _durableReader.ReadCurrentStateAsync(cancellationToken);

        if (durableIssues.Count > 0)
        {
            var events = durableIssues
                .OrderBy(i => i.Id, StringComparer.Ordinal)
                .Select(i => BuildCreateEvent(i, mergedBy))
                .Cast<IssueEvent>()
                .ToList();
            await _eventStore.AppendEventsAsync(events, cancellationToken);
        }

        _durableReader.DeleteSources();
        return durableIssues.Count;
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

    private static CreateEvent BuildCreateEvent(Issue issue, string? by)
    {
        var json = JsonSerializer.Serialize(issue, EventSourcingJsonContext.Default.Issue);
        var data = JsonDocument.Parse(json).RootElement.Clone();
        return new CreateEvent
        {
            At = issue.CreatedAt,
            By = by,
            IssueId = issue.Id,
            Data = data,
        };
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
