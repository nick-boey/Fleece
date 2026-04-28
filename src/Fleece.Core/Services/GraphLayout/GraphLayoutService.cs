using Fleece.Core.Models.Graph;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services.GraphLayout;

/// <summary>
/// Generic graph layout engine. Reproduces Fleece's leaves-first lane assignment for
/// <see cref="LayoutMode.IssueGraph"/>; reserves <see cref="LayoutMode.NormalTree"/> for a
/// follow-up change.
/// </summary>
public sealed class GraphLayoutService : IGraphLayoutService
{
    public GraphLayoutResult<TNode> Layout<TNode>(GraphLayoutRequest<TNode> request) where TNode : IGraphNode
    {
        if (request.Mode == LayoutMode.NormalTree)
        {
            throw new NotImplementedException(
                "LayoutMode.NormalTree is reserved; see openspec change add-normal-tree-layout-mode");
        }

        if (request.Mode != LayoutMode.IssueGraph)
        {
            throw new NotSupportedException($"Unsupported LayoutMode: {request.Mode}");
        }

        var ctx = new LayoutContext<TNode> { Request = request };
        var roots = request.RootFinder(request.AllNodes).ToList();

        if (request.AllNodes.Count == 0 || roots.Count == 0)
        {
            return new GraphLayoutResult<TNode>.Success(EmptyLayout<TNode>());
        }

        int maxLane = 0;
        foreach (var root in roots)
        {
            var rootMax = LayoutSubtree(root, 0, ctx);
            if (ctx.DetectedCycle != null)
            {
                return new GraphLayoutResult<TNode>.CycleDetected(ctx.DetectedCycle);
            }
            if (rootMax > maxLane)
            {
                maxLane = rootMax;
            }
        }

        if (ctx.Emitted.Count == 0)
        {
            return new GraphLayoutResult<TNode>.Success(EmptyLayout<TNode>());
        }

        int totalRows = ctx.Emitted.Count;
        int totalLanes = maxLane + 1;

        var positioned = AssignAppearanceCounts(ctx.Emitted);
        var occupancy = BuildOccupancy(positioned, ctx.Edges, totalRows, totalLanes);

        return new GraphLayoutResult<TNode>.Success(new GraphLayout<TNode>
        {
            Nodes = positioned,
            Edges = ctx.Edges,
            Occupancy = occupancy,
            TotalRows = totalRows,
            TotalLanes = totalLanes
        });
    }

    private static int LayoutSubtree<TNode>(TNode node, int startLane, LayoutContext<TNode> ctx)
        where TNode : IGraphNode
    {
        if (PathContains(ctx.PathStack, node.Id))
        {
            ctx.DetectedCycle = ExtractCycle(ctx.PathStack, node.Id);
            return -1;
        }

        if (ctx.AppearanceCounts.ContainsKey(node.Id))
        {
            Emit(node, startLane, ctx);
            return startLane;
        }

        ctx.PathStack.Push(node.Id);

        var children = ctx.Request.ChildIterator(node).ToList();

        if (children.Count == 0)
        {
            Emit(node, startLane, ctx);
            ctx.PathStack.Pop();
            return startLane;
        }

        var subtreeRoots = new List<PositionedEntry<TNode>>(children.Count);
        int maxChildLane;

        if (node.ChildSequencing == ChildSequencing.Series)
        {
            int currentLane = startLane;
            bool isFirst = true;
            foreach (var child in children)
            {
                bool emitAsLeaf = IsLeafEmission(child, ctx);
                int childStart = emitAsLeaf || isFirst ? currentLane : currentLane + 1;
                int childMax = LayoutSubtree(child, childStart, ctx);
                if (ctx.DetectedCycle != null)
                {
                    ctx.PathStack.Pop();
                    return -1;
                }
                if (!emitAsLeaf)
                {
                    currentLane = childMax;
                }
                subtreeRoots.Add(ctx.Emitted[^1]);
                isFirst = false;
            }
            maxChildLane = currentLane;
        }
        else
        {
            int max = startLane;
            foreach (var child in children)
            {
                int childMax = LayoutSubtree(child, startLane, ctx);
                if (ctx.DetectedCycle != null)
                {
                    ctx.PathStack.Pop();
                    return -1;
                }
                if (childMax > max)
                {
                    max = childMax;
                }
                subtreeRoots.Add(ctx.Emitted[^1]);
            }
            maxChildLane = max;
        }

        int parentLane = maxChildLane + 1;
        var parentEntry = Emit(node, parentLane, ctx);

        EmitEdges(node, node.ChildSequencing, subtreeRoots, parentEntry, ctx);

        ctx.PathStack.Pop();
        return parentLane;
    }

    private static PositionedEntry<TNode> Emit<TNode>(TNode node, int lane, LayoutContext<TNode> ctx)
        where TNode : IGraphNode
    {
        var entry = new PositionedEntry<TNode>
        {
            Node = node,
            Row = ctx.Emitted.Count,
            Lane = lane
        };
        ctx.Emitted.Add(entry);
        ctx.AppearanceCounts.TryGetValue(node.Id, out var count);
        ctx.AppearanceCounts[node.Id] = count + 1;
        return entry;
    }

    private static void EmitEdges<TNode>(
        TNode parent,
        ChildSequencing sequencing,
        List<PositionedEntry<TNode>> subtreeRoots,
        PositionedEntry<TNode> parentEntry,
        LayoutContext<TNode> ctx)
        where TNode : IGraphNode
    {
        if (subtreeRoots.Count == 0)
        {
            return;
        }

        if (sequencing == ChildSequencing.Series)
        {
            for (int i = 1; i < subtreeRoots.Count; i++)
            {
                var prev = subtreeRoots[i - 1];
                var curr = subtreeRoots[i];
                ctx.Edges.Add(new Edge<TNode>
                {
                    Id = $"{prev.Node.Id}->{curr.Node.Id}:{EdgeKind.SeriesSibling}#{ctx.Edges.Count}",
                    From = prev.Node,
                    To = curr.Node,
                    Kind = EdgeKind.SeriesSibling,
                    Start = new GridPosition(prev.Row, prev.Lane),
                    End = new GridPosition(curr.Row, curr.Lane),
                    PivotLane = null
                });
            }

            var last = subtreeRoots[^1];
            ctx.Edges.Add(new Edge<TNode>
            {
                Id = $"{last.Node.Id}->{parent.Id}:{EdgeKind.SeriesCornerToParent}#{ctx.Edges.Count}",
                From = last.Node,
                To = parent,
                Kind = EdgeKind.SeriesCornerToParent,
                Start = new GridPosition(last.Row, last.Lane),
                End = new GridPosition(parentEntry.Row, parentEntry.Lane),
                PivotLane = last.Lane
            });
        }
        else
        {
            foreach (var child in subtreeRoots)
            {
                ctx.Edges.Add(new Edge<TNode>
                {
                    Id = $"{child.Node.Id}->{parent.Id}:{EdgeKind.ParallelChildToSpine}#{ctx.Edges.Count}",
                    From = child.Node,
                    To = parent,
                    Kind = EdgeKind.ParallelChildToSpine,
                    Start = new GridPosition(child.Row, child.Lane),
                    End = new GridPosition(parentEntry.Row, parentEntry.Lane),
                    PivotLane = parentEntry.Lane
                });
            }
        }
    }

    private static List<PositionedNode<TNode>> AssignAppearanceCounts<TNode>(
        List<PositionedEntry<TNode>> emitted)
        where TNode : IGraphNode
    {
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in emitted)
        {
            totals.TryGetValue(e.Node.Id, out var c);
            totals[e.Node.Id] = c + 1;
        }

        var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PositionedNode<TNode>>(emitted.Count);
        foreach (var e in emitted)
        {
            indices.TryGetValue(e.Node.Id, out var idx);
            idx++;
            indices[e.Node.Id] = idx;
            result.Add(new PositionedNode<TNode>
            {
                Node = e.Node,
                Row = e.Row,
                Lane = e.Lane,
                AppearanceIndex = idx,
                TotalAppearances = totals[e.Node.Id]
            });
        }
        return result;
    }

    private static OccupancyCell[,] BuildOccupancy<TNode>(
        IReadOnlyList<PositionedNode<TNode>> nodes,
        IReadOnlyList<Edge<TNode>> edges,
        int totalRows,
        int totalLanes)
        where TNode : IGraphNode
    {
        var nodeAt = new PositionedNode<IGraphNode>?[totalRows, totalLanes];
        foreach (var n in nodes)
        {
            nodeAt[n.Row, n.Lane] = new PositionedNode<IGraphNode>
            {
                Node = n.Node,
                Row = n.Row,
                Lane = n.Lane,
                AppearanceIndex = n.AppearanceIndex,
                TotalAppearances = n.TotalAppearances
            };
        }

        var edgesAt = new List<EdgeOccupancy>?[totalRows, totalLanes];

        void Add(int r, int l, string id, EdgeSegmentKind kind)
        {
            if (r < 0 || r >= totalRows || l < 0 || l >= totalLanes)
            {
                return;
            }
            var list = edgesAt[r, l] ??= new List<EdgeOccupancy>();
            list.Add(new EdgeOccupancy { EdgeId = id, Segment = kind });
        }

        foreach (var edge in edges)
        {
            WalkEdge(edge, Add);
        }

        var result = new OccupancyCell[totalRows, totalLanes];
        for (int r = 0; r < totalRows; r++)
        {
            for (int l = 0; l < totalLanes; l++)
            {
                result[r, l] = new OccupancyCell
                {
                    Node = nodeAt[r, l],
                    Edges = (IReadOnlyList<EdgeOccupancy>?)edgesAt[r, l] ?? Array.Empty<EdgeOccupancy>()
                };
            }
        }
        return result;
    }

    private static void WalkEdge<TNode>(Edge<TNode> edge, Action<int, int, string, EdgeSegmentKind> add)
        where TNode : IGraphNode
    {
        var s = edge.Start;
        var e = edge.End;
        var id = edge.Id;

        switch (edge.Kind)
        {
            case EdgeKind.SeriesSibling:
            case EdgeKind.SeriesCornerToParent:
            {
                int pivotLane = s.Lane;

                add(s.Row, s.Lane, id, EdgeSegmentKind.Vertical);
                for (int r = s.Row + 1; r < e.Row; r++)
                {
                    add(r, pivotLane, id, EdgeSegmentKind.Vertical);
                }

                if (e.Lane == pivotLane)
                {
                    if (e.Row != s.Row)
                    {
                        add(e.Row, e.Lane, id, EdgeSegmentKind.Vertical);
                    }
                    break;
                }

                add(e.Row, pivotLane, id, EdgeSegmentKind.Horizontal);
                int step = e.Lane > pivotLane ? 1 : -1;
                for (int l = pivotLane + step; l != e.Lane; l += step)
                {
                    add(e.Row, l, id, EdgeSegmentKind.Horizontal);
                }
                add(e.Row, e.Lane, id, EdgeSegmentKind.Horizontal);
                break;
            }

            case EdgeKind.ParallelChildToSpine:
            {
                int pivotLane = e.Lane;

                if (s.Lane == pivotLane)
                {
                    add(s.Row, s.Lane, id, EdgeSegmentKind.Vertical);
                }
                else
                {
                    add(s.Row, s.Lane, id, EdgeSegmentKind.Horizontal);
                    int step = pivotLane > s.Lane ? 1 : -1;
                    for (int l = s.Lane + step; l != pivotLane; l += step)
                    {
                        add(s.Row, l, id, EdgeSegmentKind.Horizontal);
                    }
                    add(s.Row, pivotLane, id, EdgeSegmentKind.Vertical);
                }

                for (int r = s.Row + 1; r < e.Row; r++)
                {
                    add(r, pivotLane, id, EdgeSegmentKind.Vertical);
                }
                if (e.Row != s.Row)
                {
                    add(e.Row, pivotLane, id, EdgeSegmentKind.Vertical);
                }
                break;
            }
        }
    }

    private static bool IsLeafEmission<TNode>(TNode child, LayoutContext<TNode> ctx) where TNode : IGraphNode
    {
        if (ctx.AppearanceCounts.ContainsKey(child.Id))
        {
            return true;
        }
        if (PathContains(ctx.PathStack, child.Id))
        {
            return false;
        }
        return !ctx.Request.ChildIterator(child).Any();
    }

    private static bool PathContains(Stack<string> stack, string id)
    {
        foreach (var s in stack)
        {
            if (string.Equals(s, id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static IReadOnlyList<string> ExtractCycle(Stack<string> stack, string cycleNodeId)
    {
        var bottomToTop = stack.Reverse().ToList();
        var idx = bottomToTop.FindIndex(s =>
            string.Equals(s, cycleNodeId, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            return new[] { cycleNodeId, cycleNodeId };
        }
        var cycle = bottomToTop.Skip(idx).ToList();
        cycle.Add(cycleNodeId);
        return cycle;
    }

    private static GraphLayout<TNode> EmptyLayout<TNode>() where TNode : IGraphNode =>
        new()
        {
            Nodes = Array.Empty<PositionedNode<TNode>>(),
            Edges = Array.Empty<Edge<TNode>>(),
            Occupancy = new OccupancyCell[0, 0],
            TotalRows = 0,
            TotalLanes = 0
        };
}
