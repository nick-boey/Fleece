namespace Fleece.Core.Models.Graph;

/// <summary>
/// Complete layout output: positioned nodes, semantic edges, and a per-cell occupancy matrix.
/// </summary>
public sealed record GraphLayout<TNode> where TNode : IGraphNode
{
    public required IReadOnlyList<PositionedNode<TNode>> Nodes { get; init; }
    public required IReadOnlyList<Edge<TNode>> Edges { get; init; }

    /// <summary>2D matrix indexed [row, lane]. Always sized [<see cref="TotalRows"/>, <see cref="TotalLanes"/>].</summary>
    public required OccupancyCell[,] Occupancy { get; init; }

    public required int TotalRows { get; init; }
    public required int TotalLanes { get; init; }
}
