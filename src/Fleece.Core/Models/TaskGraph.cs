namespace Fleece.Core.Models;

/// <summary>
/// A positioned issue within a task graph layout.
/// </summary>
public sealed record TaskGraphNode
{
    /// <summary>
    /// The issue this node represents.
    /// </summary>
    public required Issue Issue { get; init; }

    /// <summary>
    /// Row index in the graph (0 = top, increasing downward).
    /// </summary>
    public required int Row { get; init; }

    /// <summary>
    /// Lane (column) index. Lane 0 = leftmost = next/actionable tasks.
    /// Higher lanes = further from actionable.
    /// </summary>
    public required int Lane { get; init; }

    /// <summary>
    /// Whether this issue is currently actionable (same as fleece next).
    /// </summary>
    public required bool IsActionable { get; init; }

    /// <summary>
    /// The execution mode of this node's parent in the graph.
    /// Null for root nodes (no parent in graph).
    /// Used by the renderer to determine connection style (series = vertical-first, parallel = horizontal-first).
    /// </summary>
    public ExecutionMode? ParentExecutionMode { get; init; }
}

/// <summary>
/// Complete task graph layout with positioned nodes.
/// </summary>
public sealed record TaskGraph
{
    /// <summary>
    /// All positioned nodes in row order (top to bottom).
    /// </summary>
    public required IReadOnlyList<TaskGraphNode> Nodes { get; init; }

    /// <summary>
    /// Total number of lanes (columns) used in the graph.
    /// </summary>
    public required int TotalLanes { get; init; }
}
