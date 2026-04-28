namespace Fleece.Core.Models.Graph;

/// <summary>
/// A semantic edge between two positioned nodes in a graph layout.
/// </summary>
public sealed record Edge<TNode> where TNode : IGraphNode
{
    /// <summary>Stable identifier used as a key in the occupancy matrix.</summary>
    public required string Id { get; init; }
    public required TNode From { get; init; }
    public required TNode To { get; init; }
    public required EdgeKind Kind { get; init; }
    public required GridPosition Start { get; init; }
    public required GridPosition End { get; init; }

    /// <summary>
    /// The lane at which an L-shaped edge bends. Null for kinds whose pivot is implicit
    /// (e.g. <see cref="EdgeKind.SeriesSibling"/>, where the pivot lane equals <see cref="Start"/>'s lane).
    /// </summary>
    public int? PivotLane { get; init; }

    /// <summary>Visual side of the source node where this edge attaches.</summary>
    public required EdgeAttachSide SourceAttach { get; init; }

    /// <summary>Visual side of the target node where this edge attaches.</summary>
    public required EdgeAttachSide TargetAttach { get; init; }
}

/// <summary>
/// Semantic classification of an edge, allowing renderers to differentiate by purpose.
/// </summary>
public enum EdgeKind
{
    /// <summary>Connects two consecutive children of a series parent.</summary>
    SeriesSibling,

    /// <summary>Connects the chain's last (max-lane) series child up to the parent.</summary>
    SeriesCornerToParent,

    /// <summary>Connects a parallel child rightward to the parent's lane, then down to the parent.</summary>
    ParallelChildToSpine
}

/// <summary>
/// The side of a node where an edge visually attaches. Populated by the layout engine per
/// (mode, kind) so renderers can draw without re-deriving sides from row/lane arithmetic.
/// </summary>
public enum EdgeAttachSide
{
    Top,
    Bottom,
    Left,
    Right
}
