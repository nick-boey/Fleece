using Fleece.Core.Models.Graph;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Generic graph layout engine. Pure: no I/O, no statics, reusable for any <see cref="IGraphNode"/> type.
/// </summary>
public interface IGraphLayoutService
{
    GraphLayoutResult<TNode> Layout<TNode>(GraphLayoutRequest<TNode> request) where TNode : IGraphNode;
}
