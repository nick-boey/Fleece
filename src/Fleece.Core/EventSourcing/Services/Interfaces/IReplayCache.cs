namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Reads and writes <c>.fleece/.replay-cache</c> — the gitignored cache of the
/// in-memory state after replaying snapshot plus all committed change files at the
/// recorded HEAD SHA.
/// </summary>
public interface IReplayCache
{
    /// <summary>Returns the cached state, or <c>null</c> if no cache exists or it is unparseable.</summary>
    Task<ReplayCacheFile?> TryReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the cache for the given HEAD SHA and committed-state snapshot.</summary>
    Task WriteAsync(string headSha, IReadOnlyDictionary<string, Issue> committedState, CancellationToken cancellationToken = default);

    /// <summary>Deletes the cache file if it exists.</summary>
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}
