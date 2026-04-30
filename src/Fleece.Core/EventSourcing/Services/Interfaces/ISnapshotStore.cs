using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Reads and writes the projected snapshot file <c>.fleece/issues.jsonl</c> and the
/// tombstones sidecar <c>.fleece/tombstones.jsonl</c>. Both files are written sorted
/// by ID for stable diffs.
/// </summary>
public interface ISnapshotStore
{
    /// <summary>Path of the snapshot file (<c>.fleece/issues.jsonl</c>).</summary>
    string SnapshotPath { get; }

    /// <summary>Path of the tombstones file (<c>.fleece/tombstones.jsonl</c>).</summary>
    string TombstonesPath { get; }

    /// <summary>Loads the snapshot, keyed by issue ID. Returns empty if the file does not exist.</summary>
    Task<IReadOnlyDictionary<string, Issue>> LoadSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the snapshot. One JSON object per line, sorted by ID using ordinal comparison
    /// for stable diffs across machines.
    /// </summary>
    Task WriteSnapshotAsync(IReadOnlyDictionary<string, Issue> issues, CancellationToken cancellationToken = default);

    /// <summary>Loads the tombstones sidecar. Returns empty if the file does not exist.</summary>
    Task<IReadOnlyList<Tombstone>> LoadTombstonesAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the tombstones sidecar, sorted by issue ID for stable diffs.</summary>
    Task WriteTombstonesAsync(IReadOnlyList<Tombstone> tombstones, CancellationToken cancellationToken = default);
}
