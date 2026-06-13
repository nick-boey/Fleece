using Fleece.Core.EventSourcing.Events;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services;

/// <summary>
/// Default <see cref="IReplayEngine"/>. Reads each per-issue log through
/// <see cref="IEventStore"/> and applies its events in append (line) order to a fresh
/// <see cref="IssueBuilder"/>. Files are replayed independently — there is no DAG, no
/// topological ordering, and no cross-file tiebreak.
/// </summary>
public sealed class ReplayEngine : IReplayEngine
{
    private readonly IEventStore _eventStore;

    public ReplayEngine(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<IReadOnlyDictionary<string, Issue>> ReplayAsync(
        IReadOnlyList<string> logFilePaths,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, Issue>(StringComparer.Ordinal);

        foreach (var path in logFilePaths)
        {
            var events = await _eventStore.ReadIssueLogAsync(path, cancellationToken);
            var builder = new IssueBuilder();
            var created = false;
            foreach (var evt in events)
            {
                switch (evt)
                {
                    case CreateEvent c:
                        builder.ApplyCreate(c);
                        created = true;
                        break;
                    case SetEvent s:
                        builder.ApplySet(s);
                        break;
                    case AddEvent a:
                        builder.ApplyAdd(a);
                        break;
                    case RemoveEvent r:
                        builder.ApplyRemove(r);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown event type at apply time: {evt.GetType().FullName}");
                }
            }

            // A log without a create event projects no issue (defensive: ignore it).
            if (created)
            {
                result[builder.Id] = builder.ToIssue();
            }
        }

        return result;
    }
}
