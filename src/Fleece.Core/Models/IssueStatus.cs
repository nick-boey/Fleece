namespace Fleece.Core.Models;

/// <summary>
/// Workflow status of an issue.
/// </summary>
public enum IssueStatus
{
    /// <summary>
    /// Issue is in draft state, not yet fully specified.
    /// </summary>
    Draft,

    /// <summary>
    /// Issue is open and available to be worked on.
    /// Replaces the previous Idea, Spec, and Next statuses.
    /// </summary>
    Open,

    /// <summary>
    /// Issue is currently being worked on.
    /// </summary>
    Progress,

    /// <summary>
    /// Issue is under review.
    /// </summary>
    Review,

    /// <summary>
    /// Work is complete, awaiting verification.
    /// </summary>
    Complete,

    /// <summary>
    /// Issue is no longer relevant.
    /// </summary>
    Archived,

    /// <summary>
    /// Issue has been verified as complete.
    /// </summary>
    Closed,

    /// <summary>
    /// Issue has been deleted (soft delete).
    /// </summary>
    Deleted
}

/// <summary>
/// Extension methods for IssueStatus.
/// </summary>
public static class IssueStatusExtensions
{
    /// <summary>
    /// Statuses that indicate an issue is "done" for dependency purposes.
    /// </summary>
    public static readonly IssueStatus[] DoneStatuses =
        [IssueStatus.Complete, IssueStatus.Archived, IssueStatus.Closed];

    /// <summary>
    /// Terminal statuses that are excluded from listings by default.
    /// </summary>
    public static readonly IssueStatus[] TerminalStatuses =
        [IssueStatus.Complete, IssueStatus.Archived, IssueStatus.Closed, IssueStatus.Deleted];

    /// <summary>
    /// Returns true if the status indicates the issue is "done" for dependency resolution.
    /// </summary>
    public static bool IsDone(this IssueStatus status) =>
        status is IssueStatus.Complete or IssueStatus.Archived or IssueStatus.Closed;

    /// <summary>
    /// Returns true if the status is a terminal status (excluded from listings by default).
    /// </summary>
    public static bool IsTerminal(this IssueStatus status) =>
        status is IssueStatus.Complete or IssueStatus.Archived or IssueStatus.Closed or IssueStatus.Deleted;

    /// <summary>
    /// Returns true if the status indicates the issue is actionable (can be worked on).
    /// </summary>
    public static bool IsActionable(this IssueStatus status) =>
        status is IssueStatus.Open;
}
