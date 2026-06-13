namespace Fleece.Core.Models;

/// <summary>
/// Workflow status of an issue.
/// </summary>
public enum IssueStatus
{
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
    /// Issue has been escalated to a GitHub issue. Terminal; carries a
    /// <c>promoted=&lt;github-#&gt;</c> keyed tag.
    /// </summary>
    Promoted,

    /// <summary>
    /// Issue has been verified as complete.
    /// </summary>
    Closed
}

/// <summary>
/// Extension methods for IssueStatus.
/// </summary>
public static class IssueStatusExtensions
{
    /// <summary>
    /// The active set: a branch is not mergeable/sealable while any issue holds one of these.
    /// </summary>
    public static readonly IssueStatus[] ActiveStatuses =
        [IssueStatus.Open, IssueStatus.Progress, IssueStatus.Review];

    /// <summary>
    /// The inactive set: terminal statuses that do not block seal/merge.
    /// </summary>
    public static readonly IssueStatus[] InactiveStatuses =
        [IssueStatus.Complete, IssueStatus.Closed, IssueStatus.Promoted];

    /// <summary>
    /// Statuses that indicate an issue is "done" for dependency purposes.
    /// </summary>
    public static readonly IssueStatus[] DoneStatuses =
        [IssueStatus.Complete, IssueStatus.Closed, IssueStatus.Promoted];

    /// <summary>
    /// Terminal statuses that are excluded from listings by default.
    /// </summary>
    public static readonly IssueStatus[] TerminalStatuses =
        [IssueStatus.Complete, IssueStatus.Closed, IssueStatus.Promoted];

    /// <summary>
    /// Returns true if the status is in the active set ({Open, Progress, Review}).
    /// </summary>
    public static bool IsActive(this IssueStatus status) =>
        status is IssueStatus.Open or IssueStatus.Progress or IssueStatus.Review;

    /// <summary>
    /// Returns true if the status indicates the issue is "done" for dependency resolution.
    /// </summary>
    public static bool IsDone(this IssueStatus status) =>
        status is IssueStatus.Complete or IssueStatus.Closed or IssueStatus.Promoted;

    /// <summary>
    /// Returns true if the status is a terminal status (excluded from listings by default).
    /// </summary>
    public static bool IsTerminal(this IssueStatus status) =>
        status is IssueStatus.Complete or IssueStatus.Closed or IssueStatus.Promoted;

    /// <summary>
    /// Returns true if the status indicates the issue is actionable (can be worked on).
    /// </summary>
    public static bool IsActionable(this IssueStatus status) =>
        status is IssueStatus.Open;
}
