using Fleece.Core.EventSourcing.Events;
using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Read/write surface over the event-sourced layout: snapshot + change files.
/// Reads return the in-memory state computed by replaying the snapshot plus all
/// change files (with replay-cache acceleration when available). Writes append
/// events to the active change file.
/// </summary>
public interface IEventSourcedStorageService
{
    /// <summary>Returns the current in-memory state, computed via snapshot + replay.</summary>
    Task<IReadOnlyDictionary<string, Issue>> GetIssuesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the persisted tombstones list (read directly from the sidecar file).</summary>
    Task<IReadOnlyList<Tombstone>> GetTombstonesAsync(CancellationToken cancellationToken = default);

    /// <summary>Appends one or more events to the active change file (rotates if necessary).</summary>
    Task AppendEventsAsync(IReadOnlyList<IssueEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the snapshot file with the given dictionary and rewrites the tombstones
    /// sidecar with the given list. Used by <c>fleece project</c> at compaction time;
    /// not invoked by ordinary write commands.
    /// </summary>
    Task WriteSnapshotAsync(
        IReadOnlyDictionary<string, Issue> issues,
        IReadOnlyList<Tombstone> tombstones,
        CancellationToken cancellationToken = default);

    /// <summary>Lists every change file currently present (full paths). Useful for projection.</summary>
    Task<IReadOnlyList<string>> GetAllChangeFilePathsAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes every change file in <c>.fleece/changes/</c>. Used by <c>fleece project</c>.</summary>
    Task DeleteAllChangeFilesAsync(CancellationToken cancellationToken = default);
}
