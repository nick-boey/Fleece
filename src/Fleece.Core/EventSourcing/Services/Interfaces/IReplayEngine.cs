using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Replays the per-issue append-only logs and produces the resulting in-memory issue
/// dictionary. Each log file is replayed independently in its own append order; there
/// is no cross-file ordering, no DAG, and no tiebreak.
/// </summary>
public interface IReplayEngine
{
    /// <summary>
    /// Replays each of the given per-issue log files independently and returns the
    /// resulting dictionary keyed by issue id.
    /// </summary>
    /// <param name="logFilePaths">Paths of <c>.fleece/issues/*.jsonl</c> files to replay.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<string, Issue>> ReplayAsync(
        IReadOnlyList<string> logFilePaths,
        CancellationToken cancellationToken = default);
}
