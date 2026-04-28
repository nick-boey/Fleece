namespace Fleece.Core.Models.Graph;

/// <summary>
/// One cell of the occupancy matrix. A cell may carry both a node and one or more edge segments.
/// </summary>
public sealed record OccupancyCell
{
    /// <summary>
    /// The node placed at this cell, if any. Typed covariantly so any layout's node type fits.
    /// </summary>
    public PositionedNode<IGraphNode>? Node { get; init; }

    /// <summary>Every edge segment passing through this cell.</summary>
    public required IReadOnlyList<EdgeOccupancy> Edges { get; init; }
}

/// <summary>
/// Records that a particular edge intersects a cell with a particular geometric segment.
/// </summary>
public sealed record EdgeOccupancy
{
    public required string EdgeId { get; init; }
    public required EdgeSegmentKind Segment { get; init; }
}

/// <summary>
/// How an edge intersects a single cell. Renderers use this to choose glyphs.
/// </summary>
public enum EdgeSegmentKind
{
    Vertical,
    Horizontal,
    CornerNE,
    CornerNW,
    CornerSE,
    CornerSW,
    JunctionT_East,
    JunctionT_West,
    JunctionT_North,
    JunctionT_South
}
