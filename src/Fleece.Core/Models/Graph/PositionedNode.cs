namespace Fleece.Core.Models.Graph;

/// <summary>
/// A node placed at a specific (row, lane) within a graph layout.
/// </summary>
public sealed record PositionedNode<TNode> where TNode : IGraphNode
{
    public required TNode Node { get; init; }
    public required int Row { get; init; }
    public required int Lane { get; init; }

    /// <summary>1-based occurrence index for nodes that appear multiple times (multi-parent fan-in).</summary>
    public int AppearanceIndex { get; init; } = 1;

    /// <summary>Total times this node appears in the layout. Equals 1 for single-parent nodes.</summary>
    public int TotalAppearances { get; init; } = 1;
}
