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
    private readonly IWarningSink _warningSink;

    public ReplayEngine(IEventStore eventStore, IWarningSink? warningSink = null)
    {
        _eventStore = eventStore;
        _warningSink = warningSink ?? NullWarningSink.Instance;
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
    /// Multi-parent nodes (merge markers) wait until every parent has been emitted.
    /// Dangling <c>follows</c> entries are silently dropped; a file with all-dangling
    /// follows becomes a DAG root.
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
            // Defensive: STJ source-gen may bypass the custom converter for null JSON
            // values, leaving the IReadOnlyList property null instead of empty.
            var follows = meta.Follows ?? Array.Empty<string>();
            nodes.Add(new FileNode(guid, path, follows, commitOrder.GetFirstCommitOrdinal(path)));
        }

        var byGuid = nodes.ToDictionary(n => n.Guid, StringComparer.Ordinal);

        // Drop dangling follows entries (referenced GUIDs that aren't present locally).
        for (var i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n.Follows.Count == 0)
            {
                continue;
            }
            var live = n.Follows.Where(byGuid.ContainsKey).ToList();
            if (live.Count != n.Follows.Count)
            {
                nodes[i] = n with { Follows = live };
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
            foreach (var parent in n.Follows)
            {
                children[parent].Add(n.Guid);
                inDegree[n.Guid]++;
            }
        }

        // Marker semantics: when a multi-parent node lists parents as [P1, P2, ...]
        // the ORDER of that list is a load-bearing signal — P1 must be emitted before
        // P2, P2 before P3, etc. Add explicit Pi → Pi+1 edges so the topological sort
        // respects merge ordering even after a squash collapses commit ordinals.
        // We skip edges that would create a self-loop or duplicate an existing edge,
        // and silently skip edges that would introduce a cycle (defensive).
        foreach (var n in nodes)
        {
            if (n.Follows.Count < 2)
            {
                continue;
            }
            for (var i = 0; i < n.Follows.Count - 1; i++)
            {
                var from = n.Follows[i];
                var to = n.Follows[i + 1];
                if (string.Equals(from, to, StringComparison.Ordinal))
                {
                    continue;
                }
                if (children[from].Contains(to, StringComparer.Ordinal))
                {
                    continue;
                }
                if (CreatesCycle(from, to, children))
                {
                    continue;
                }
                children[from].Add(to);
                inDegree[to]++;
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
            // Before consuming `first`, check whether any other equally-ready node
            // shares its commit ordinal — if so, emit the parallel-files warning.
            EmitParallelTiebreakWarningIfNeeded(first, ready, byGuid, children);
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

    /// <summary>
    /// When two simultaneously-ready nodes share the same non-null commit ordinal and
    /// no later file in the DAG names both as parents, replay is about to fall through
    /// to GUID-alphabetical ordering — surface a warning so operators can run
    /// <c>fleece link --merge</c> against the responsible commit.
    /// </summary>
    private void EmitParallelTiebreakWarningIfNeeded(
        string first,
        SortedSet<string> ready,
        Dictionary<string, FileNode> byGuid,
        Dictionary<string, List<string>> children)
    {
        if (ready.Count < 2)
        {
            return;
        }
        var firstNode = byGuid[first];
        if (firstNode.CommitOrdinal is null)
        {
            return;
        }
        foreach (var other in ready)
        {
            if (ReferenceEquals(other, first) || string.Equals(other, first, StringComparison.Ordinal))
            {
                continue;
            }
            var otherNode = byGuid[other];
            if (otherNode.CommitOrdinal != firstNode.CommitOrdinal)
            {
                continue;
            }
            if (HasCommonMarkerDescendant(first, other, byGuid, children))
            {
                continue;
            }
            _warningSink.Warn(
                $"change files {System.IO.Path.GetFileName(firstNode.Path)} and {System.IO.Path.GetFileName(otherNode.Path)} " +
                $"share commit ordinal {firstNode.CommitOrdinal.Value} and have no merge marker linearizing them. " +
                "Ordering by GUID is non-semantic; consider running `fleece link --merge` against the responsible merge commit.");
        }
    }

    /// <summary>
    /// Returns true if adding an edge <paramref name="from"/> → <paramref name="to"/>
    /// would create a cycle in the existing children graph (i.e., <paramref name="to"/>
    /// can already reach <paramref name="from"/>).
    /// </summary>
    private static bool CreatesCycle(
        string from, string to, Dictionary<string, List<string>> children)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(to);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (string.Equals(current, from, StringComparison.Ordinal))
            {
                return true;
            }
            if (!visited.Add(current))
            {
                continue;
            }
            if (children.TryGetValue(current, out var next))
            {
                foreach (var child in next)
                {
                    stack.Push(child);
                }
            }
        }
        return false;
    }

    /// <summary>
    /// True if some node downstream of both <paramref name="a"/> and <paramref name="b"/>
    /// names both of them as parents — i.e., a marker already covers this parallel pair.
    /// </summary>
    private static bool HasCommonMarkerDescendant(
        string a,
        string b,
        Dictionary<string, FileNode> byGuid,
        Dictionary<string, List<string>> children)
    {
        // Build the set of descendants of `a` (BFS), then check whether any of them
        // names `b` as one of its parents. Symmetric: if the marker exists at all
        // it shows up under whichever side we walk.
        var descendantsOfA = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(a);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in children[current])
            {
                if (descendantsOfA.Add(child))
                {
                    queue.Enqueue(child);
                }
            }
        }
        foreach (var d in descendantsOfA)
        {
            if (byGuid[d].Follows.Contains(b, StringComparer.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private sealed record FileNode(string Guid, string Path, IReadOnlyList<string> Follows, int? CommitOrdinal);

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
