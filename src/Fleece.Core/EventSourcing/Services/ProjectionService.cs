using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services;

/// <summary>
/// Default <see cref="IProjectionService"/>. Replays the snapshot plus all change
/// files into a fresh in-memory state, applies 30-day auto-cleanup to soft-deleted
/// issues, writes the new snapshot/tombstones, and deletes every change file.
/// </summary>
public sealed class ProjectionService : IProjectionService
{
    /// <summary>
    /// How long an issue must remain in <see cref="IssueStatus.Deleted"/> with no further
    /// modifications before <c>fleece project</c> hard-deletes it.
    /// </summary>
    public static readonly TimeSpan AutoCleanupAge = TimeSpan.FromDays(30);

    private readonly IEventSourcedStorageService _storage;

    public ProjectionService(IEventSourcedStorageService storage)
    {
        _storage = storage;
    }

    public async Task<ProjectionResult> ProjectAsync(
        DateTimeOffset now,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var changeFiles = await _storage.GetAllChangeFilePathsAsync(cancellationToken);
        var issues = await _storage.GetIssuesAsync(cancellationToken);
        var existingTombstones = await _storage.GetTombstonesAsync(cancellationToken);

        var threshold = now - AutoCleanupAge;
        var keep = new Dictionary<string, Issue>(StringComparer.Ordinal);
        var newTombstones = new List<Tombstone>();
        foreach (var (id, issue) in issues)
        {
            // Heuristic: an issue is auto-cleanable when it is currently Deleted AND it has
            // had no modifications in the auto-cleanup window. issue.LastUpdate bounds when
            // status could have been set to Deleted, so it's a conservative proxy for the
            // strict "status was set ≥30d ago" rule.
            if (issue.Status == IssueStatus.Deleted && issue.LastUpdate <= threshold)
            {
                newTombstones.Add(new Tombstone
                {
                    IssueId = issue.Id,
                    OriginalTitle = issue.Title,
                    CleanedAt = now,
                    CleanedBy = actor,
                });
            }
            else
            {
                keep[id] = issue;
            }
        }

        var allTombstones = MergeTombstones(existingTombstones, newTombstones);
        await _storage.WriteSnapshotAsync(keep, allTombstones, cancellationToken);
        await _storage.DeleteAllChangeFilesAsync(cancellationToken);

        return new ProjectionResult
        {
            IssueCount = keep.Count,
            ChangeFilesCompacted = changeFiles.Count,
            AutoCleanedTombstones = newTombstones,
        };
    }

    private static IReadOnlyList<Tombstone> MergeTombstones(
        IReadOnlyList<Tombstone> existing,
        IReadOnlyList<Tombstone> added)
    {
        if (added.Count == 0)
        {
            return existing;
        }
        // Deduplicate by IssueId. Newer tombstones (from this run) win on conflict.
        var byId = existing.ToDictionary(t => t.IssueId, StringComparer.Ordinal);
        foreach (var t in added)
        {
            byId[t.IssueId] = t;
        }
        return byId.Values.OrderBy(t => t.IssueId, StringComparer.Ordinal).ToList();
    }
}
