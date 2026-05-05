using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Compacts the event-sourced state into a fresh snapshot. Used by
/// <c>fleece project</c> to fold all change files into <c>.fleece/issues.jsonl</c>
/// (and <c>.fleece/tombstones.jsonl</c> for hard-deletes), and to apply 30-day
/// auto-cleanup for soft-deleted issues.
/// </summary>
public interface IProjectionService
{
    /// <summary>
    /// Runs a projection. Reads the snapshot, replays every change file, applies
    /// 30-day auto-cleanup for soft-deleted issues, writes the new snapshot and
    /// tombstones, and deletes every file under <c>.fleece/changes/</c>.
    /// </summary>
    /// <param name="now">
    /// The reference time used to evaluate the 30-day auto-cleanup window and to
    /// stamp <c>cleanedAt</c> on new tombstones. Injectable so tests can pin time.
    /// </param>
    /// <param name="actor">
    /// The user identity recorded as <c>cleanedBy</c> on auto-cleanup tombstones.
    /// </param>
    Task<ProjectionResult> ProjectAsync(
        DateTimeOffset now,
        string actor,
        CancellationToken cancellationToken = default);
}

/// <summary>Summary of what a projection run produced.</summary>
public sealed record ProjectionResult
{
    /// <summary>Number of issues in the new snapshot.</summary>
    public required int IssueCount { get; init; }

    /// <summary>Number of change files that were folded into the snapshot.</summary>
    public required int ChangeFilesCompacted { get; init; }

    /// <summary>Tombstones added by 30-day auto-cleanup during this run.</summary>
    public required IReadOnlyList<Tombstone> AutoCleanedTombstones { get; init; }
}
