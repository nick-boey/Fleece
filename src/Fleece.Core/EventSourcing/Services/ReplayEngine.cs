using Fleece.Core.EventSourcing.Events;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services;

/// <summary>
/// Default <see cref="IReplayEngine"/>. Reads events through <see cref="IEventStore"/>,
/// computes a topological order over the follows-DAG (with commit-ordinal and GUID
/// alphabetical tiebreaks), and applies events to a mutable in-memory state.
/// </summary>
public sealed class ReplayEngine : IReplayEngine
{
    private readonly IEventStore _eventStore;

    public ReplayEngine(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<IReadOnlyDictionary<string, Issue>> ReplayAsync(
        IReadOnlyDictionary<string, Issue> initialState,
        IReadOnlyList<string> changeFilePaths,
        IChangeFileCommitOrder? commitOrder = null,
        CancellationToken cancellationToken = default)
    {
        var ordering = commitOrder ?? NullChangeFileCommitOrder.Instance;
        var builders = new Dictionary<string, IssueBuilder>(StringComparer.Ordinal);
        foreach (var (id, issue) in initialState)
        {
            builders[id] = IssueBuilder.FromIssue(issue);
        }

        if (changeFilePaths.Count == 0)
        {
            return BuildersToDictionary(builders);
        }

        var sorted = await SortChangeFilesAsync(changeFilePaths, ordering, cancellationToken);

        foreach (var node in sorted)
        {
            var events = await _eventStore.ReadChangeFileAsync(node.Path, cancellationToken);
            // Skip the leading meta event; only apply mutating events.
            for (var i = 1; i < events.Count; i++)
            {
                ApplyEvent(builders, events[i]);
            }
        }

        return BuildersToDictionary(builders);
    }

    private static IReadOnlyDictionary<string, Issue> BuildersToDictionary(Dictionary<string, IssueBuilder> builders) =>
        builders.ToDictionary(kv => kv.Key, kv => kv.Value.ToIssue(), StringComparer.Ordinal);

    private static void ApplyEvent(Dictionary<string, IssueBuilder> builders, IssueEvent evt)
    {
        switch (evt)
        {
            case CreateEvent c:
                {
                    if (!builders.TryGetValue(c.IssueId, out var existing))
                    {
                        existing = new IssueBuilder();
                        builders[c.IssueId] = existing;
                    }
                    existing.ApplyCreate(c);
                    break;
                }
            case SetEvent s:
                if (builders.TryGetValue(s.IssueId, out var sb))
                {
                    sb.ApplySet(s);
                }
                break;
            case AddEvent a:
                if (builders.TryGetValue(a.IssueId, out var ab))
                {
                    ab.ApplyAdd(a);
                }
                break;
            case RemoveEvent r:
                if (builders.TryGetValue(r.IssueId, out var rb))
                {
                    rb.ApplyRemove(r);
                }
                break;
            case HardDeleteEvent h:
                builders.Remove(h.IssueId);
                break;
            case MetaEvent:
                // Meta events are positioning-only; ignored during application.
                break;
            default:
                throw new InvalidOperationException($"Unknown event type at apply time: {evt.GetType().FullName}");
        }
    }

    /// <summary>
    /// Builds the follows-DAG over the given files and returns them in topological order.
    /// Tiebreaks: commit ordinal (smaller first; null = last), then GUID alphabetical.
    /// Dangling <c>follows</c> pointers are silently treated as <c>null</c> (the file becomes
    /// a DAG root).
    /// </summary>
    private async Task<IReadOnlyList<FileNode>> SortChangeFilesAsync(
        IReadOnlyList<string> paths,
        IChangeFileCommitOrder commitOrder,
        CancellationToken cancellationToken)
    {
        var nodes = new List<FileNode>(paths.Count);
        foreach (var path in paths)
        {
            var meta = await _eventStore.ReadMetaAsync(path, cancellationToken);
            var guid = EventStore.ExtractGuidFromPath(path);
            nodes.Add(new FileNode(guid, path, meta.Follows, commitOrder.GetFirstCommitOrdinal(path)));
        }

        var byGuid = nodes.ToDictionary(n => n.Guid, StringComparer.Ordinal);

        // Resolve dangling follows pointers down to null.
        for (var i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n.Follows is not null && !byGuid.ContainsKey(n.Follows))
            {
                nodes[i] = n with { Follows = null };
            }
        }
        byGuid = nodes.ToDictionary(n => n.Guid, StringComparer.Ordinal);

        var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var n in nodes)
        {
            inDegree[n.Guid] = 0;
            children[n.Guid] = [];
        }
        foreach (var n in nodes)
        {
            if (n.Follows is not null)
            {
                children[n.Follows].Add(n.Guid);
                inDegree[n.Guid]++;
            }
        }

        var comparer = new FileNodeReadyComparer(byGuid);
        var ready = new SortedSet<string>(comparer);
        foreach (var (guid, deg) in inDegree)
        {
            if (deg == 0)
            {
                ready.Add(guid);
            }
        }

        var result = new List<FileNode>(nodes.Count);
        while (ready.Count > 0)
        {
            var first = ready.Min!;
            ready.Remove(first);
            result.Add(byGuid[first]);
            foreach (var child in children[first])
            {
                if (--inDegree[child] == 0)
                {
                    ready.Add(child);
                }
            }
        }

        if (result.Count != nodes.Count)
        {
            // Defensive: a cycle would leave nodes unemitted. Append by alphabetical order.
            var emitted = result.Select(n => n.Guid).ToHashSet(StringComparer.Ordinal);
            foreach (var n in nodes.OrderBy(node => node.Guid, StringComparer.Ordinal))
            {
                if (!emitted.Contains(n.Guid))
                {
                    result.Add(n);
                }
            }
        }

        return result;
    }

    private sealed record FileNode(string Guid, string Path, string? Follows, int? CommitOrdinal);

    private sealed class FileNodeReadyComparer(Dictionary<string, FileNode> byGuid) : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x is null)
            {
                return y is null ? 0 : -1;
            }
            if (y is null)
            {
                return 1;
            }
            var nx = byGuid[x];
            var ny = byGuid[y];
            var ox = nx.CommitOrdinal ?? int.MaxValue;
            var oy = ny.CommitOrdinal ?? int.MaxValue;
            var cmp = ox.CompareTo(oy);
            return cmp != 0 ? cmp : StringComparer.Ordinal.Compare(x, y);
        }
    }
}
