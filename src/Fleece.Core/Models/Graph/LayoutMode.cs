namespace Fleece.Core.Models.Graph;

/// <summary>
/// Strategy for assigning rows and lanes to nodes during layout.
/// </summary>
public enum LayoutMode
{
    /// <summary>
    /// Leaves-first row order with leaf-upward lane assignment. Mirrors Fleece's classic task graph.
    /// </summary>
    IssueGraph,

    /// <summary>
    /// Parent-first row order with depth-from-root lane assignment. Roots sit at lane 0, every
    /// child is placed at <c>parent.lane + 1</c>, and siblings share their parent's child-lane —
    /// they are separated only by row order so subtrees naturally don't collide horizontally.
    /// Mirrors <see cref="IssueGraph"/> over the row axis: the natural shape for top-down trees.
    /// </summary>
    NormalTree
}
