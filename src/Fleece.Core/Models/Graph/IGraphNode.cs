namespace Fleece.Core.Models.Graph;

/// <summary>
/// Minimal contract for a node consumed by <c>IGraphLayoutService</c>.
/// </summary>
public interface IGraphNode
{
    string Id { get; }
    ChildSequencing ChildSequencing { get; }
}

/// <summary>
/// How a node's children are sequenced relative to each other when laid out.
/// </summary>
public enum ChildSequencing
{
    Series,
    Parallel
}
