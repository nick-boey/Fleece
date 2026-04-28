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
    /// Reserved for the follow-up change <c>add-normal-tree-layout-mode</c>; not yet implemented.
    /// </summary>
    NormalTree
}
