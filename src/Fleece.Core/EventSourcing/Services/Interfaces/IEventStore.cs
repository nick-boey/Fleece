using Fleece.Core.EventSourcing.Events;

namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Read/write surface over the per-session change files in <c>.fleece/changes/</c>.
/// Owns the active-pointer rotation rules described in the event-sourced-storage capability.
/// </summary>
public interface IEventStore
{
    /// <summary>Lists every change file currently present in <c>.fleece/changes/</c> (full paths).</summary>
    Task<IReadOnlyList<string>> GetAllChangeFilePathsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and parses every event in a change file. The first event MUST be a
    /// <see cref="MetaEvent"/>; if it is not, an exception is thrown.
    /// </summary>
    Task<IReadOnlyList<IssueEvent>> ReadChangeFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Reads only the first-line meta event of the given change file.</summary>
    Task<MetaEvent> ReadMetaAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the path of the currently active change file, or <c>null</c> if there is
    /// no active session yet (the next append will rotate to a fresh GUID).
    /// </summary>
    Task<string?> GetActiveChangeFilePathAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends one or more events to the active change file. Rotates to a fresh change file
    /// if the active pointer is missing or its referenced file no longer exists on disk.
    /// Returns the absolute path of the file the events were appended to.
    /// </summary>
    Task<string> AppendEventsAsync(IReadOnlyList<IssueEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces creation of a new change file with a fresh GUID, writing the meta event with
    /// <c>follows</c> set to the current DAG leaf (or <c>null</c> if no change files exist).
    /// Updates <c>.fleece/.active-change</c> to point at the new file. Returns the new file's path.
    /// </summary>
    Task<string> RotateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every change file in <c>.fleece/changes/</c> and clears the active pointer.
    /// Used by <c>fleece project</c> after compacting events into the snapshot.
    /// </summary>
    Task DeleteAllChangeFilesAsync(CancellationToken cancellationToken = default);
}
