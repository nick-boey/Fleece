namespace Fleece.Core.Models.Graph;

/// <summary>
/// Result of <c>IGraphLayoutService.Layout</c>. Either a complete layout or a cycle report.
/// </summary>
public abstract record GraphLayoutResult<TNode> where TNode : IGraphNode
{
    private GraphLayoutResult() { }

    public sealed record Success(GraphLayout<TNode> Layout) : GraphLayoutResult<TNode>;

    /// <summary>
    /// A cycle was detected during traversal. <see cref="Cycle"/> is the ordered list of node ids
    /// from the prior occurrence on the path stack to the cycle-closing re-entry, inclusive.
    /// </summary>
    public sealed record CycleDetected(IReadOnlyList<string> Cycle) : GraphLayoutResult<TNode>;
}
