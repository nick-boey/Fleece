namespace Fleece.Core.Models;

/// <summary>
/// Context about a parent issue including execution mode, position in series, and adjacent siblings.
/// </summary>
public sealed record ParentContextDto
{
    public required IssueSummaryDto Parent { get; init; }
    public required ExecutionMode ExecutionMode { get; init; }

    /// <summary>
    /// 1-based position within the parent's children. Null if parent uses Parallel execution mode.
    /// </summary>
    public int? Position { get; init; }

    /// <summary>
    /// Total number of siblings under this parent. Null if parent uses Parallel execution mode.
    /// </summary>
    public int? TotalSiblings { get; init; }

    /// <summary>
    /// The preceding sibling in sort order. Null if first child or parent uses Parallel execution mode.
    /// </summary>
    public IssueSummaryDto? PreviousSibling { get; init; }

    /// <summary>
    /// The next sibling in sort order. Null if last child or parent uses Parallel execution mode.
    /// </summary>
    public IssueSummaryDto? NextSibling { get; init; }
}
