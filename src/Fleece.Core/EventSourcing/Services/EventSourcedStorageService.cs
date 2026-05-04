using Fleece.Core.EventSourcing.Events;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services;

/// <summary>
/// Default <see cref="IEventSourcedStorageService"/>. Coordinates snapshot store,
/// event store, replay engine, replay cache, and git context to produce reads
/// (cached when possible) and route writes through event append.
/// </summary>
public sealed class EventSourcedStorageService : IEventSourcedStorageService
{
    private readonly ISnapshotStore _snapshot;
    private readonly IEventStore _eventStore;
    private readonly IReplayEngine _replayEngine;
    private readonly IReplayCache _replayCache;
    private readonly IEventGitContext _gitContext;

    public EventSourcedStorageService(
        ISnapshotStore snapshot,
        IEventStore eventStore,
        IReplayEngine replayEngine,
        IReplayCache replayCache,
        IEventGitContext gitContext)
    {
        _snapshot = snapshot;
        _eventStore = eventStore;
        _replayEngine = replayEngine;
        _replayCache = replayCache;
        _gitContext = gitContext;
    }

    public async Task<IReadOnlyDictionary<string, Issue>> GetIssuesAsync(CancellationToken cancellationToken = default)
    {
        var allChangeFiles = await _eventStore.GetAllChangeFilePathsAsync(cancellationToken);

        var headSha = _gitContext.GetHeadSha();

        // Partition change files into committed-at-HEAD vs uncommitted (active session, staged, untracked).
        var committedFiles = new List<string>();
        var uncommittedFiles = new List<string>();
        foreach (var path in allChangeFiles)
        {
            if (_gitContext.IsFileCommittedAtHead(path))
            {
                committedFiles.Add(path);
            }
            else
            {
                uncommittedFiles.Add(path);
            }
        }

        IReadOnlyDictionary<string, Issue> committedState;

        if (headSha is not null)
        {
            var cached = await _replayCache.TryReadAsync(cancellationToken);
            if (cached is not null && string.Equals(cached.HeadSha, headSha, StringComparison.Ordinal))
            {
                // Cache hit: trust the cached committed state, replay only uncommitted on top.
                committedState = cached.Issues.ToDictionary(i => i.Id, StringComparer.Ordinal);
            }
            else
            {
                committedState = await ComputeCommittedStateAsync(committedFiles, cancellationToken);
                await _replayCache.WriteAsync(headSha, committedState, cancellationToken);
            }
        }
        else
        {
            // Not in a git repo (or unborn HEAD) — caching is disabled, always full replay.
            committedState = await ComputeCommittedStateAsync(committedFiles, cancellationToken);
        }

        if (uncommittedFiles.Count == 0)
        {
            return committedState;
        }

        return await _replayEngine.ReplayAsync(committedState, uncommittedFiles, _gitContext, cancellationToken);
    }

    public Task<IReadOnlyList<Tombstone>> GetTombstonesAsync(CancellationToken cancellationToken = default) =>
        _snapshot.LoadTombstonesAsync(cancellationToken);

    public async Task AppendEventsAsync(IReadOnlyList<IssueEvent> events, CancellationToken cancellationToken = default)
    {
        await _eventStore.AppendEventsAsync(events, cancellationToken);
    }

    public async Task WriteSnapshotAsync(
        IReadOnlyDictionary<string, Issue> issues,
        IReadOnlyList<Tombstone> tombstones,
        CancellationToken cancellationToken = default)
    {
        await _snapshot.WriteSnapshotAsync(issues, cancellationToken);
        await _snapshot.WriteTombstonesAsync(tombstones, cancellationToken);
        await _replayCache.InvalidateAsync(cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetAllChangeFilePathsAsync(CancellationToken cancellationToken = default) =>
        _eventStore.GetAllChangeFilePathsAsync(cancellationToken);

    public async Task DeleteAllChangeFilesAsync(CancellationToken cancellationToken = default)
    {
        await _eventStore.DeleteAllChangeFilesAsync(cancellationToken);
        await _replayCache.InvalidateAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, Issue>> ComputeCommittedStateAsync(
        IReadOnlyList<string> committedFiles,
        CancellationToken cancellationToken)
    {
        var snapshot = await _snapshot.LoadSnapshotAsync(cancellationToken);
        if (committedFiles.Count == 0)
        {
            return snapshot;
        }
        return await _replayEngine.ReplayAsync(snapshot, committedFiles, _gitContext, cancellationToken);
    }
}
