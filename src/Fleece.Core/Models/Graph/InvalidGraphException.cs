namespace Fleece.Core.Models.Graph;

/// <summary>
/// Thrown by adapters (e.g. <c>IIssueLayoutService</c>) when the underlying graph contains a cycle
/// and the caller cannot meaningfully continue without a clean DAG.
/// </summary>
public sealed class InvalidGraphException : Exception
{
    public IReadOnlyList<string> Cycle { get; }

    public InvalidGraphException(IReadOnlyList<string> cycle)
        : base($"Graph contains a cycle: {string.Join(" -> ", cycle)}")
    {
        Cycle = cycle;
    }
}
