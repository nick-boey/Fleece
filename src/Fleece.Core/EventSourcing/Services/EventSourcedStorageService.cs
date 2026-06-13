using Fleece.Core.EventSourcing.Events;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services;

/// <summary>
/// Default <see cref="IEventSourcedStorageService"/>. Coordinates the event store and
/// replay engine: reads replay every per-issue log independently; writes append events
/// to the target issues' logs; deletes remove a log file.
/// </summary>
public sealed class EventSourcedStorageService : IEventSourcedStorageService
{
    private readonly IEventStore _eventStore;
    private readonly IReplayEngine _replayEngine;

    public EventSourcedStorageService(
        IEventStore eventStore,
        IReplayEngine replayEngine)
    {
        _eventStore = eventStore;
        _replayEngine = replayEngine;
    }

    public async Task<IReadOnlyDictionary<string, Issue>> GetIssuesAsync(CancellationToken cancellationToken = default)
    {
        var logPaths = await _eventStore.GetAllIssueLogPathsAsync(cancellationToken);
        return await _replayEngine.ReplayAsync(logPaths, cancellationToken);
    }

    public Task AppendEventsAsync(IReadOnlyList<IssueEvent> events, CancellationToken cancellationToken = default) =>
        _eventStore.AppendEventsAsync(events, cancellationToken);

    public Task DeleteIssueAsync(string issueId, CancellationToken cancellationToken = default) =>
        _eventStore.DeleteIssueLogAsync(issueId, cancellationToken);
}
