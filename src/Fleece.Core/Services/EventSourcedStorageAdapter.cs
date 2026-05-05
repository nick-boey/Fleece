using System.IO.Abstractions;
using System.Text.Json;
using Fleece.Core.EventSourcing;
using Fleece.Core.EventSourcing.Events;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Adapts the event-sourced storage layer to the legacy <see cref="IStorageService"/>
/// surface. Reads come from snapshot+replay; writes are translated into event
/// emissions on the active change file.
/// </summary>
/// <remarks>
/// Hashed-file methods (<c>GetAllIssueFilesAsync</c>, <c>SaveIssuesWithHashAsync</c>, etc.)
/// return empty/no-op results — they describe the legacy multi-machine layout that
/// no longer exists. Tombstone writes go directly to the snapshot sidecar via
/// <see cref="ISnapshotStore"/>; ordinary <c>fleece project</c> rebuilds the sidecar
/// during compaction.
/// </remarks>
internal sealed class EventSourcedStorageAdapter : IStorageService
{
    private readonly IEventSourcedStorageService _eventSourced;
    private readonly ISnapshotStore _snapshot;
    private readonly IGitConfigService _gitConfig;
    private readonly string _basePath;
    private readonly IFileSystem _fileSystem;

    public EventSourcedStorageAdapter(
        IEventSourcedStorageService eventSourced,
        ISnapshotStore snapshot,
        IGitConfigService gitConfig,
        string basePath,
        IFileSystem fileSystem)
    {
        _eventSourced = eventSourced;
        _snapshot = snapshot;
        _gitConfig = gitConfig;
        _basePath = basePath;
        _fileSystem = fileSystem;
    }

    public async Task<IReadOnlyList<Issue>> LoadIssuesAsync(CancellationToken cancellationToken = default)
    {
        var dict = await _eventSourced.GetIssuesAsync(cancellationToken);
        // Order by CreatedAt so consumers that rely on file-append (creation) order
        // continue to receive issues in the same sequence they were created. ID is the
        // stable tiebreaker for issues that share a timestamp.
        return dict.Values
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id, StringComparer.Ordinal)
            .ToList();
    }

    public async Task SaveIssuesAsync(IReadOnlyList<Issue> issues, CancellationToken cancellationToken = default)
    {
        var current = await _eventSourced.GetIssuesAsync(cancellationToken);
        var by = _gitConfig.GetUserName();
        var events = new List<IssueEvent>();

        var newById = issues.ToDictionary(i => i.Id, StringComparer.Ordinal);

        // Emit hard-delete for issues present before but absent now.
        foreach (var oldId in current.Keys)
        {
            if (!newById.ContainsKey(oldId))
            {
                events.Add(new HardDeleteEvent
                {
                    At = DateTimeOffset.UtcNow,
                    By = by,
                    IssueId = oldId,
                });
            }
        }

        foreach (var issue in issues)
        {
            if (!current.TryGetValue(issue.Id, out var existing))
            {
                events.Add(BuildCreateEvent(issue, by));
            }
            else
            {
                events.AddRange(DiffEvents(existing, issue, by));
            }
        }

        if (events.Count > 0)
        {
            await _eventSourced.AppendEventsAsync(events, cancellationToken);
        }
    }

    public async Task AppendIssueAsync(Issue issue, CancellationToken cancellationToken = default)
    {
        var by = _gitConfig.GetUserName();
        await _eventSourced.AppendEventsAsync([BuildCreateEvent(issue, by)], cancellationToken);
    }

    public Task EnsureDirectoryExistsAsync(CancellationToken cancellationToken = default)
    {
        var dir = _fileSystem.Path.Combine(_basePath, ".fleece");
        if (!_fileSystem.Directory.Exists(dir))
        {
            _fileSystem.Directory.CreateDirectory(dir);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetAllIssueFilesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<Issue>> LoadIssuesFromFileAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Issue>>([]);

    public Task DeleteIssueFileAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<string> SaveIssuesWithHashAsync(IReadOnlyList<Issue> issues, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Empty);

    public Task<(bool HasMultiple, string Message)> HasMultipleUnmergedFilesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult((false, string.Empty));

    public async Task<LoadIssuesResult> LoadIssuesWithDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        var issues = await LoadIssuesAsync(cancellationToken);
        return new LoadIssuesResult
        {
            Issues = issues,
            Diagnostics = []
        };
    }

    public Task<IReadOnlyList<Tombstone>> LoadTombstonesAsync(CancellationToken cancellationToken = default) =>
        _eventSourced.GetTombstonesAsync(cancellationToken);

    public async Task SaveTombstonesAsync(IReadOnlyList<Tombstone> tombstones, CancellationToken cancellationToken = default)
    {
        await _snapshot.WriteTombstonesAsync(tombstones, cancellationToken);
    }

    public async Task AppendTombstonesAsync(IReadOnlyList<Tombstone> tombstones, CancellationToken cancellationToken = default)
    {
        var existing = await _eventSourced.GetTombstonesAsync(cancellationToken);
        var combined = existing.Concat(tombstones).ToList();
        await _snapshot.WriteTombstonesAsync(combined, cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetAllTombstoneFilesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

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

    private static IEnumerable<IssueEvent> DiffEvents(Issue before, Issue after, string? by)
    {
        var at = after.LastUpdate;

        if (!string.Equals(before.Title, after.Title, StringComparison.Ordinal))
        {
            yield return BuildSetEvent(at, by, after.Id, "title", after.Title);
        }
        if (!string.Equals(before.Description, after.Description, StringComparison.Ordinal))
        {
            yield return BuildSetEvent(at, by, after.Id, "description", after.Description);
        }
        if (before.Status != after.Status)
        {
            yield return BuildSetEvent(at, by, after.Id, "status", after.Status.ToString());
        }
        if (before.Type != after.Type)
        {
            yield return BuildSetEvent(at, by, after.Id, "type", after.Type.ToString());
        }
        if (before.LinkedPR != after.LinkedPR)
        {
            yield return BuildSetEventNumber(at, by, after.Id, "linkedPR", after.LinkedPR);
        }
        if (before.Priority != after.Priority)
        {
            yield return BuildSetEventNumber(at, by, after.Id, "priority", after.Priority);
        }
        if (!string.Equals(before.AssignedTo, after.AssignedTo, StringComparison.Ordinal))
        {
            yield return BuildSetEvent(at, by, after.Id, "assignedTo", after.AssignedTo);
        }
        if (!string.Equals(before.WorkingBranchId, after.WorkingBranchId, StringComparison.Ordinal))
        {
            yield return BuildSetEvent(at, by, after.Id, "workingBranchId", after.WorkingBranchId);
        }
        if (before.ExecutionMode != after.ExecutionMode)
        {
            yield return BuildSetEvent(at, by, after.Id, "executionMode", after.ExecutionMode.ToString());
        }
        if (!string.Equals(before.CreatedBy, after.CreatedBy, StringComparison.Ordinal))
        {
            yield return BuildSetEvent(at, by, after.Id, "createdBy", after.CreatedBy);
        }

        foreach (var evt in DiffStringArray(at, by, after.Id, "linkedIssues", before.LinkedIssues, after.LinkedIssues))
        {
            yield return evt;
        }
        foreach (var evt in DiffStringArray(at, by, after.Id, "tags", before.Tags, after.Tags))
        {
            yield return evt;
        }
        foreach (var evt in DiffParentIssues(at, by, after.Id, before.ParentIssues, after.ParentIssues))
        {
            yield return evt;
        }
    }

    private static SetEvent BuildSetEvent(DateTimeOffset at, string? by, string issueId, string property, string? value)
    {
        var json = value is null ? "null" : EncodeJsonString(value);
        return new SetEvent
        {
            At = at,
            By = by,
            IssueId = issueId,
            Property = property,
            Value = JsonDocument.Parse(json).RootElement.Clone(),
        };
    }

    private static SetEvent BuildSetEventNumber(DateTimeOffset at, string? by, string issueId, string property, int? value)
    {
        var json = value.HasValue ? value.Value.ToString() : "null";
        return new SetEvent
        {
            At = at,
            By = by,
            IssueId = issueId,
            Property = property,
            Value = JsonDocument.Parse(json).RootElement.Clone(),
        };
    }

    private static IEnumerable<IssueEvent> DiffStringArray(
        DateTimeOffset at,
        string? by,
        string issueId,
        string property,
        IReadOnlyList<string> before,
        IReadOnlyList<string> after)
    {
        var beforeSet = new HashSet<string>(before, StringComparer.Ordinal);
        var afterSet = new HashSet<string>(after, StringComparer.Ordinal);

        foreach (var added in after.Where(v => !beforeSet.Contains(v)))
        {
            yield return new AddEvent
            {
                At = at,
                By = by,
                IssueId = issueId,
                Property = property,
                Value = JsonDocument.Parse(EncodeJsonString(added)).RootElement.Clone(),
            };
        }
        foreach (var removed in before.Where(v => !afterSet.Contains(v)))
        {
            yield return new RemoveEvent
            {
                At = at,
                By = by,
                IssueId = issueId,
                Property = property,
                Value = JsonDocument.Parse(EncodeJsonString(removed)).RootElement.Clone(),
            };
        }
    }

    private static IEnumerable<IssueEvent> DiffParentIssues(
        DateTimeOffset at,
        string? by,
        string issueId,
        IReadOnlyList<ParentIssueRef> before,
        IReadOnlyList<ParentIssueRef> after)
    {
        var beforeByKey = before.GroupBy(p => p.ParentIssue, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var afterByKey = after.GroupBy(p => p.ParentIssue, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Removed parent refs.
        foreach (var (key, _) in beforeByKey)
        {
            if (!afterByKey.ContainsKey(key))
            {
                yield return BuildParentRemoveEvent(at, by, issueId, key);
            }
        }

        // Added or changed parent refs — emit add (which upserts by key in the replay engine).
        foreach (var (key, afterRef) in afterByKey)
        {
            if (!beforeByKey.TryGetValue(key, out var beforeRef) || !ParentRefEquals(beforeRef, afterRef))
            {
                yield return BuildParentAddEvent(at, by, issueId, afterRef);
            }
        }
    }

    private static bool ParentRefEquals(ParentIssueRef a, ParentIssueRef b) =>
        string.Equals(a.ParentIssue, b.ParentIssue, StringComparison.Ordinal)
        && string.Equals(a.SortOrder, b.SortOrder, StringComparison.Ordinal)
        && a.Active == b.Active;

    private static AddEvent BuildParentAddEvent(DateTimeOffset at, string? by, string issueId, ParentIssueRef value)
    {
        var json = JsonSerializer.Serialize(value, EventSourcingJsonContext.Default.ParentIssueRef);
        return new AddEvent
        {
            At = at,
            By = by,
            IssueId = issueId,
            Property = "parentIssues",
            Value = JsonDocument.Parse(json).RootElement.Clone(),
        };
    }

    private static RemoveEvent BuildParentRemoveEvent(DateTimeOffset at, string? by, string issueId, string parentKey)
    {
        var json = $"{{\"parentIssue\":{EncodeJsonString(parentKey)}}}";
        return new RemoveEvent
        {
            At = at,
            By = by,
            IssueId = issueId,
            Property = "parentIssues",
            Value = JsonDocument.Parse(json).RootElement.Clone(),
        };
    }

    private static string EncodeJsonString(string value)
    {
        // Build a JSON string literal without going through the reflection-based
        // JsonSerializer.Serialize(string) overload (which is not AOT-safe).
        var bufferWriter = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(bufferWriter))
        {
            writer.WriteStringValue(value);
        }
        return System.Text.Encoding.UTF8.GetString(bufferWriter.ToArray());
    }
}
