namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Applies a sequence of change files on top of an initial snapshot state and produces
/// the resulting in-memory issue dictionary. Encapsulates the DAG topo sort, tiebreaks,
/// and event-application semantics described in the event-sourced-storage capability.
/// </summary>
public interface IReplayEngine
{
    /// <summary>
    /// Replays the given change files over <paramref name="initialState"/>. Returns the
    /// resulting in-memory dictionary keyed by issue ID. <paramref name="initialState"/>
    /// is left untouched.
    /// </summary>
    /// <param name="initialState">Starting state (typically loaded from <c>.fleece/issues.jsonl</c>).</param>
    /// <param name="changeFilePaths">Paths of change files to replay. Order is computed by the engine.</param>
    /// <param name="commitOrder">
    /// Optional commit-order tiebreak source. Pass <see cref="NullChangeFileCommitOrder.Instance"/>
    /// to skip commit-order tiebreaks (DAG topo + GUID alphabetical only).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<string, Issue>> ReplayAsync(
        IReadOnlyDictionary<string, Issue> initialState,
        IReadOnlyList<string> changeFilePaths,
        IChangeFileCommitOrder? commitOrder = null,
        CancellationToken cancellationToken = default);
}
