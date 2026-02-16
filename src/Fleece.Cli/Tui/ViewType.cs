namespace Fleece.Cli.Tui;

/// <summary>
/// Defines the active view in the TUI.
/// </summary>
public enum ViewType
{
    /// <summary>
    /// Main issue list with search and filtering.
    /// </summary>
    IssueList,

    /// <summary>
    /// Read-only detail view of a single issue.
    /// </summary>
    IssueDetail,

    /// <summary>
    /// Edit form for modifying an existing issue.
    /// </summary>
    IssueEdit,

    /// <summary>
    /// Create form for a new issue.
    /// </summary>
    CreateIssue
}
