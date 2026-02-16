using Fleece.Core.Models;

namespace Fleece.Cli.Tui;

/// <summary>
/// Application state container for the TUI, using reactive State&lt;T&gt; properties.
/// </summary>
public sealed class TuiAppState
{
    /// <summary>
    /// Cached list of all issues.
    /// </summary>
    public IReadOnlyList<Issue> Issues { get; set; } = [];

    /// <summary>
    /// Filtered list of issues (after search/filter applied).
    /// </summary>
    public IReadOnlyList<Issue> FilteredIssues { get; set; } = [];

    /// <summary>
    /// Currently selected issue index.
    /// </summary>
    public int SelectedIndex { get; set; }

    /// <summary>
    /// Currently selected issue in the list.
    /// </summary>
    public Issue? SelectedIssue => FilteredIssues.Count > 0 && SelectedIndex < FilteredIssues.Count
        ? FilteredIssues[SelectedIndex]
        : null;

    /// <summary>
    /// The active view being displayed.
    /// </summary>
    public ViewType CurrentView { get; set; } = ViewType.IssueList;

    /// <summary>
    /// Current search query for filtering issues.
    /// </summary>
    public string SearchQuery { get; set; } = string.Empty;

    /// <summary>
    /// Whether to include terminal statuses (Complete, Archived, Closed, Deleted) in the list.
    /// </summary>
    public bool IncludeTerminal { get; set; }

    /// <summary>
    /// Status message to display in the footer.
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Whether there are pending saves.
    /// </summary>
    public bool HasPendingSaves { get; set; }

    /// <summary>
    /// The issue currently being edited (for edit/create views).
    /// </summary>
    public Issue? EditingIssue { get; set; }

    /// <summary>
    /// Scroll offset for detail/edit views.
    /// </summary>
    public int ScrollOffset { get; set; }

    /// <summary>
    /// Current field index in edit view.
    /// </summary>
    public int EditFieldIndex { get; set; }

    /// <summary>
    /// Error message in edit view.
    /// </summary>
    public string? EditError { get; set; }

    /// <summary>
    /// Whether to exit the application.
    /// </summary>
    public bool ShouldExit { get; set; }

    /// <summary>
    /// Whether we're in create mode (vs edit mode).
    /// </summary>
    public bool IsCreateMode { get; set; }

    // Edit form field values
    public string EditTitle { get; set; } = "";
    public string EditDescription { get; set; } = "";
    public IssueStatus EditStatus { get; set; } = IssueStatus.Open;
    public IssueType EditType { get; set; } = IssueType.Task;
    public string EditPriority { get; set; } = "";
    public string EditAssignedTo { get; set; } = "";
    public string EditWorkingBranch { get; set; } = "";
    public string EditLinkedPR { get; set; } = "";
    public ExecutionMode EditExecutionMode { get; set; } = ExecutionMode.Series;
    public string EditTags { get; set; } = "";
    public string EditLinkedIssues { get; set; } = "";
    public string EditParentIssues { get; set; } = "";

    /// <summary>
    /// Applies the search filter to the issues list.
    /// </summary>
    public void ApplyFilter()
    {
        var query = SearchQuery.Trim();

        FilteredIssues = Issues
            .Where(i => IncludeTerminal || !i.Status.IsTerminal())
            .Where(i => string.IsNullOrEmpty(query) ||
                        i.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        (i.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        i.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                        i.Id.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.Priority ?? 99)
            .ThenBy(i => i.Title)
            .ToList();

        // Clamp selection
        if (SelectedIndex >= FilteredIssues.Count)
        {
            SelectedIndex = Math.Max(0, FilteredIssues.Count - 1);
        }
    }

    /// <summary>
    /// Loads the editing issue's values into the edit fields.
    /// </summary>
    public void LoadEditFields()
    {
        if (IsCreateMode)
        {
            EditTitle = "";
            EditDescription = "";
            EditStatus = IssueStatus.Open;
            EditType = IssueType.Task;
            EditPriority = "";
            EditAssignedTo = "";
            EditWorkingBranch = "";
            EditLinkedPR = "";
            EditExecutionMode = ExecutionMode.Series;
            EditTags = "";
            EditLinkedIssues = "";
            EditParentIssues = "";
            return;
        }

        if (EditingIssue == null)
        {
            return;
        }

        EditTitle = EditingIssue.Title;
        EditDescription = EditingIssue.Description ?? "";
        EditStatus = EditingIssue.Status;
        EditType = EditingIssue.Type;
        EditPriority = EditingIssue.Priority?.ToString() ?? "";
        EditAssignedTo = EditingIssue.AssignedTo ?? "";
        EditWorkingBranch = EditingIssue.WorkingBranchId ?? "";
        EditLinkedPR = EditingIssue.LinkedPR?.ToString() ?? "";
        EditExecutionMode = EditingIssue.ExecutionMode;
        EditTags = string.Join(", ", EditingIssue.Tags);
        EditLinkedIssues = string.Join(", ", EditingIssue.LinkedIssues);
        EditParentIssues = string.Join(", ", EditingIssue.ParentIssues.Select(p => $"{p.ParentIssue}:{p.SortOrder}"));
    }
}
