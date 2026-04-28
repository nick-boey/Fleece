namespace Fleece.Core.Models.Graph;

/// <summary>
/// Inputs for <c>IGraphLayoutService.Layout</c>. The two delegates carry topology;
/// <see cref="Mode"/> selects the lane/row assignment strategy.
/// </summary>
public sealed record GraphLayoutRequest<TNode> where TNode : IGraphNode
{
    public required IReadOnlyList<TNode> AllNodes { get; init; }
    public required Func<IReadOnlyList<TNode>, IEnumerable<TNode>> RootFinder { get; init; }
    public required Func<TNode, IEnumerable<TNode>> ChildIterator { get; init; }
    public LayoutMode Mode { get; init; } = LayoutMode.IssueGraph;
}
