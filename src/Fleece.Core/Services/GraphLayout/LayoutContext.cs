using Fleece.Core.Models.Graph;

namespace Fleece.Core.Services.GraphLayout;

/// <summary>
/// Mutable per-call state shared by the layout engine and its lane strategy.
/// </summary>
internal sealed class LayoutContext<TNode> where TNode : IGraphNode
{
    public required GraphLayoutRequest<TNode> Request { get; init; }

    /// <summary>Nodes in emission order (children first, then parent).</summary>
    public List<PositionedEntry<TNode>> Emitted { get; } = new();

    /// <summary>Edges produced during traversal.</summary>
    public List<Edge<TNode>> Edges { get; } = new();

    /// <summary>Active DFS path; presence indicates a cycle on re-entry.</summary>
    public Stack<string> PathStack { get; } = new();

    /// <summary>Cumulative emission count per node id, used for stable edge IDs.</summary>
    public Dictionary<string, int> AppearanceCounts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string>? DetectedCycle { get; set; }
}

/// <summary>
/// One emission of a node into the layout, before final appearance counts are assigned.
/// </summary>
internal sealed record PositionedEntry<TNode> where TNode : IGraphNode
{
    public required TNode Node { get; init; }
    public required int Row { get; init; }
    public required int Lane { get; init; }
}
