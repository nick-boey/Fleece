namespace Fleece.Core.Models;

/// <summary>
/// A node in the issue graph representing an issue with its computed relationships.
/// </summary>
public sealed record IssueGraphNode
{
    /// <summary>
    /// The issue this node represents.
    /// </summary>
    public required Issue Issue { get; init; }

    /// <summary>
    /// IDs of immediate child issues (issues that have this issue as a parent).
    /// </summary>
    public required IReadOnlyList<string> ChildIssueIds { get; init; }

    /// <summary>
    /// IDs of immediate parent issues (from ParentIssues that exist in the graph).
    /// </summary>
    public required IReadOnlyList<string> ParentIssueIds { get; init; }

    /// <summary>
    /// IDs of issues that must complete BEFORE this issue can be worked on.
    /// Only populated for siblings under a Series execution mode parent.
    /// For Parallel mode siblings and root issues, this list is EMPTY.
    /// </summary>
    public required IReadOnlyList<string> PreviousIssueIds { get; init; }

    /// <summary>
    /// IDs of issues that come AFTER this issue (blocked by this issue).
    /// Only populated for siblings under a Series execution mode parent.
    /// For Parallel mode siblings and root issues, this list is EMPTY.
    /// </summary>
    public required IReadOnlyList<string> NextIssueIds { get; init; }

    /// <summary>
    /// True if this issue has incomplete children (cannot be completed yet).
    /// </summary>
    public required bool HasIncompleteChildren { get; init; }

    /// <summary>
    /// True if all PreviousIssueIds are in a "done" status.
    /// </summary>
    public required bool AllPreviousDone { get; init; }

    /// <summary>
    /// The execution mode of the parent that controls this node's series/parallel behavior.
    /// Null for root nodes (no parent in graph).
    /// </summary>
    public ExecutionMode? ParentExecutionMode { get; init; }
}

/// <summary>
/// Complete issue graph with all computed relationships.
/// </summary>
public sealed record IssueGraph
{
    /// <summary>
    /// All nodes in the graph keyed by issue ID (case-insensitive).
    /// </summary>
    public required IReadOnlyDictionary<string, IssueGraphNode> Nodes { get; init; }

    /// <summary>
    /// IDs of root issues (no parent in the graph).
    /// </summary>
    public required IReadOnlyList<string> RootIssueIds { get; init; }

    /// <summary>
    /// Gets a node by issue ID, or null if not found.
    /// </summary>
    public IssueGraphNode? GetNode(string issueId) =>
        Nodes.TryGetValue(issueId, out var node) ? node : null;
}

/// <summary>
/// Query parameters for building/filtering an issue graph.
/// </summary>
public sealed record GraphQuery
{
    /// <summary>
    /// Optional root issue ID to scope the graph to a subtree.
    /// When specified, only the root and its descendants are included.
    /// The root issue itself IS included in the result.
    /// </summary>
    public string? RootIssueId { get; init; }

    /// <summary>
    /// Filter by status. Null means no status filter.
    /// </summary>
    public IssueStatus? Status { get; init; }

    /// <summary>
    /// Filter by type. Null means no type filter.
    /// </summary>
    public IssueType? Type { get; init; }

    /// <summary>
    /// Filter by priority. Null means no priority filter.
    /// </summary>
    public int? Priority { get; init; }

    /// <summary>
    /// Filter by assignee. Null means no assignee filter.
    /// </summary>
    public string? AssignedTo { get; init; }

    /// <summary>
    /// Filter by tags. Issues must have at least one matching tag.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Filter by linked PR. Null means no PR filter.
    /// </summary>
    public int? LinkedPr { get; init; }

    /// <summary>
    /// Free-text search across title, description, and tags.
    /// </summary>
    public string? SearchText { get; init; }

    /// <summary>
    /// When true, includes terminal issues (Complete, Archived, Closed, Deleted).
    /// Default is false.
    /// </summary>
    public bool IncludeTerminal { get; init; }

    /// <summary>
    /// When true, includes terminal issues that have active (non-terminal) descendants.
    /// This allows showing parent context even when the parent is marked complete.
    /// Default is false.
    /// </summary>
    public bool IncludeInactiveWithActiveDescendants { get; init; }
}
