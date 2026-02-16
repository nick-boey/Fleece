using Fleece.Cli.Tui.Styles;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using XenoAtom.Ansi;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Styling;

namespace Fleece.Cli.Tui;

/// <summary>
/// Main TUI application using XenoAtom.Terminal.UI.
/// </summary>
public sealed class TuiApp
{
    private readonly IFleeceInMemoryService _issueService;
    private readonly TuiAppState _state;

    public TuiApp(IFleeceInMemoryService issueService)
    {
        _issueService = issueService;
        _state = new TuiAppState();
    }

    public async Task<int> RunAsync()
    {
        // Load issues into state
        await LoadIssuesAsync();
        _state.ApplyFilter();

        // Open terminal session
        using var session = Terminal.Open();

        // Run the terminal loop
        Terminal.Run(BuildUI(), OnUpdate);

        return 0;
    }

    private TerminalLoopResult OnUpdate()
    {
        if (_state.ShouldExit)
        {
            return TerminalLoopResult.StopAndKeepVisual;
        }
        return TerminalLoopResult.Continue;
    }

    private Visual BuildUI()
    {
        // Use a simple VStack layout
        return new VStack(
            // Header
            new TextBlock(() => $" Fleece Issue Tracker - {_state.FilteredIssues.Count} issues "),

            // Content - rebuild based on current view
            new Group(() => BuildContent()),

            // Footer
            new TextBlock(() => BuildFooterText())
        );
    }

    private string BuildFooterText()
    {
        var shortcuts = _state.CurrentView switch
        {
            ViewType.IssueList => "↑↓:Navigate | Enter:View | N:New | T:Toggle Terminal | Q:Quit",
            ViewType.IssueDetail => "Esc:Back | E:Edit",
            ViewType.IssueEdit or ViewType.CreateIssue => "Tab:Next | Ctrl+S:Save | Esc:Cancel",
            _ => ""
        };

        return $" {_state.StatusMessage} | {shortcuts} ";
    }

    private Visual BuildContent()
    {
        return _state.CurrentView switch
        {
            ViewType.IssueList => BuildIssueList(),
            ViewType.IssueDetail => BuildIssueDetail(),
            ViewType.IssueEdit or ViewType.CreateIssue => BuildIssueEdit(),
            _ => new TextBlock("Unknown view")
        };
    }

    private Visual BuildIssueList()
    {
        // Build table rows
        var rows = new List<Visual>();

        // Header row
        rows.Add(new TextBlock("ID       Title                                      Type       Status       Pri  Assigned"));
        rows.Add(new TextBlock("─".PadRight(90, '─')));

        // Data rows
        foreach (var issue in _state.FilteredIssues)
        {
            var id = issue.Id[..Math.Min(6, issue.Id.Length)];
            var title = TruncateText(issue.Title, 40);

            var row = $"{id.PadRight(8)} {title.PadRight(42)} {issue.Type.ToString().PadRight(10)} {issue.Status.ToString().PadRight(12)} {(issue.Priority?.ToString() ?? "-").PadRight(4)} {issue.AssignedTo ?? "-"}";
            rows.Add(new TextBlock(row));
        }

        var countLabel = new TextBlock(() =>
        {
            var count = $"\n{_state.FilteredIssues.Count} issues";
            if (_state.IncludeTerminal)
            {
                count += " [+terminal]";
            }

            return count;
        });

        return new VStack(rows.Concat([countLabel]).ToArray())
            .KeyDown((_, e) =>
            {
                if (e.Key == TerminalKey.Up && _state.SelectedIndex > 0)
                {
                    _state.SelectedIndex--;
                }
                else if (e.Key == TerminalKey.Down && _state.SelectedIndex < _state.FilteredIssues.Count - 1)
                {
                    _state.SelectedIndex++;
                }
                else if (e.Key == TerminalKey.Enter && _state.SelectedIssue != null)
                {
                    _state.EditingIssue = _state.SelectedIssue;
                    _state.CurrentView = ViewType.IssueDetail;
                    _state.ScrollOffset = 0;
                }
                else if (e.Key == (TerminalKey)'N')
                {
                    _state.IsCreateMode = true;
                    _state.EditingIssue = null;
                    _state.LoadEditFields();
                    _state.CurrentView = ViewType.CreateIssue;
                    _state.EditFieldIndex = 0;
                }
                else if (e.Key == (TerminalKey)'T')
                {
                    _state.IncludeTerminal = !_state.IncludeTerminal;
                    _ = ReloadAndFilterAsync();
                }
                else if (e.Key == (TerminalKey)'Q')
                {
                    _state.ShouldExit = true;
                }
            });
    }

    private Visual BuildIssueDetail()
    {
        var issue = _state.EditingIssue;
        if (issue == null)
        {
            return new TextBlock("No issue selected");
        }

        var lines = new List<Visual>
        {
            new TextBlock($"─── Issue: {issue.Id[..Math.Min(6, issue.Id.Length)]} ───"),
            new TextBlock(""),
            BuildPropertyRow("Title", issue.Title),
            BuildPropertyRow("Status", issue.Status.ToString()),
            BuildPropertyRow("Type", issue.Type.ToString()),
            BuildPropertyRow("Priority", issue.Priority?.ToString() ?? "-"),
            BuildPropertyRow("Assigned To", issue.AssignedTo ?? "-"),
            BuildPropertyRow("Execution Mode", issue.ExecutionMode.ToString()),
            BuildPropertyRow("Working Branch", issue.WorkingBranchId ?? "-"),
            BuildPropertyRow("Linked PR", issue.LinkedPR?.ToString() ?? "-"),
            new TextBlock("")
        };

        // Description
        if (!string.IsNullOrWhiteSpace(issue.Description))
        {
            lines.Add(new TextBlock("─── Description ───"));
            lines.Add(new TextBlock(issue.Description));
            lines.Add(new TextBlock(""));
        }

        // Relationships
        if (issue.LinkedIssues.Count > 0 || issue.ParentIssues.Count > 0)
        {
            lines.Add(new TextBlock("─── Relationships ───"));
            if (issue.LinkedIssues.Count > 0)
            {
                lines.Add(BuildPropertyRow("Linked Issues", string.Join(", ", issue.LinkedIssues)));
            }

            if (issue.ParentIssues.Count > 0)
            {
                lines.Add(BuildPropertyRow("Parent Issues", string.Join(", ", issue.ParentIssues.Select(p => $"{p.ParentIssue}:{p.SortOrder}"))));
            }

            lines.Add(new TextBlock(""));
        }

        // Tags
        if (issue.Tags.Count > 0)
        {
            lines.Add(new TextBlock("─── Tags ───"));
            lines.Add(BuildPropertyRow("Tags", string.Join(", ", issue.Tags)));
            lines.Add(new TextBlock(""));
        }

        // Questions
        if (issue.Questions.Count > 0)
        {
            lines.Add(new TextBlock($"─── Questions ({issue.Questions.Count}) ───"));
            foreach (var q in issue.Questions)
            {
                var answered = q.Answer != null;
                var indicator = answered ? "[✓]" : "[?]";
                lines.Add(new TextBlock($"  {indicator} {q.Id}: {q.Text}"));
                if (q.Answer != null)
                {
                    lines.Add(new TextBlock($"      {q.Answer}"));
                }
            }
            lines.Add(new TextBlock(""));
        }

        // Audit
        lines.Add(new TextBlock("─── Audit ───"));
        lines.Add(BuildPropertyRow("Created At", issue.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")));
        lines.Add(BuildPropertyRow("Created By", issue.CreatedBy ?? "-"));
        lines.Add(BuildPropertyRow("Last Update", issue.LastUpdate.ToString("yyyy-MM-dd HH:mm:ss")));

        return new ScrollViewer(new VStack(lines.ToArray()))
            .KeyDown((_, e) =>
            {
                if (e.Key == TerminalKey.Escape || e.Key == TerminalKey.Backspace)
                {
                    _state.CurrentView = ViewType.IssueList;
                }
                else if (e.Key == (TerminalKey)'E')
                {
                    _state.IsCreateMode = false;
                    _state.LoadEditFields();
                    _state.CurrentView = ViewType.IssueEdit;
                    _state.EditFieldIndex = 0;
                }
            });
    }

    private static Visual BuildPropertyRow(string label, string value)
    {
        return new TextBlock($"  {label.PadRight(16)}: {value}");
    }

    private Visual BuildIssueEdit()
    {
        var title = _state.IsCreateMode ? "Create New Issue" : "Edit Issue";

        var lines = new List<Visual>
        {
            new TextBlock($"─── {title} ───"),
            new TextBlock(""),

            new TextBlock($"  Title*          : {_state.EditTitle}"),
            new TextBlock($"  Description     : {_state.EditDescription}"),
            new TextBlock($"  Status          : {_state.EditStatus}"),
            new TextBlock($"  Type            : {_state.EditType}"),
            new TextBlock($"  Priority        : {_state.EditPriority}"),
            new TextBlock($"  Assigned To     : {_state.EditAssignedTo}"),
            new TextBlock($"  Working Branch  : {_state.EditWorkingBranch}"),
            new TextBlock($"  Linked PR       : {_state.EditLinkedPR}"),
            new TextBlock($"  Execution Mode  : {_state.EditExecutionMode}"),
            new TextBlock($"  Tags            : {_state.EditTags}"),
            new TextBlock($"  Linked Issues   : {_state.EditLinkedIssues}"),
            new TextBlock($"  Parent Issues   : {_state.EditParentIssues}"),
        };

        if (!string.IsNullOrEmpty(_state.EditError))
        {
            lines.Add(new TextBlock($"\nError: {_state.EditError}"));
        }

        return new ScrollViewer(new VStack(lines.ToArray()))
            .KeyDown((_, e) =>
            {
                if (e.Key == TerminalKey.Escape)
                {
                    _state.EditError = null;
                    _state.CurrentView = _state.IsCreateMode ? ViewType.IssueList : ViewType.IssueDetail;
                }
                else if (e.Key == TerminalKey.Tab)
                {
                    if ((e.Modifiers & TerminalModifiers.Shift) != 0)
                    {
                        _state.EditFieldIndex = (_state.EditFieldIndex - 1 + 12) % 12;
                    }
                    else
                    {
                        _state.EditFieldIndex = (_state.EditFieldIndex + 1) % 12;
                    }
                }
                else if (e.Key == (TerminalKey)'S' && (e.Modifiers & TerminalModifiers.Ctrl) != 0)
                {
                    _ = SaveIssueAsync();
                }
            });
    }

    private async Task SaveIssueAsync()
    {
        _state.EditError = null;

        // Validate
        if (string.IsNullOrWhiteSpace(_state.EditTitle))
        {
            _state.EditError = "Title is required";
            return;
        }

        int? priority = null;
        if (!string.IsNullOrWhiteSpace(_state.EditPriority))
        {
            if (!int.TryParse(_state.EditPriority, out var p) || p < 1 || p > 5)
            {
                _state.EditError = "Priority must be 1-5";
                return;
            }
            priority = p;
        }

        int? linkedPr = null;
        if (!string.IsNullOrWhiteSpace(_state.EditLinkedPR))
        {
            if (!int.TryParse(_state.EditLinkedPR, out var pr))
            {
                _state.EditError = "Linked PR must be a number";
                return;
            }
            linkedPr = pr;
        }

        var tags = string.IsNullOrWhiteSpace(_state.EditTags)
            ? new List<string>()
            : _state.EditTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var linkedIssues = string.IsNullOrWhiteSpace(_state.EditLinkedIssues)
            ? new List<string>()
            : _state.EditLinkedIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var parentIssues = ParentIssueRef.ParseFromStrings(_state.EditParentIssues);

        try
        {
            _state.StatusMessage = "Saving...";
            _state.HasPendingSaves = true;

            if (_state.IsCreateMode)
            {
                var newIssue = await _issueService.CreateIssueAsync(
                    title: _state.EditTitle.Trim(),
                    type: _state.EditType,
                    description: string.IsNullOrWhiteSpace(_state.EditDescription) ? null : _state.EditDescription,
                    priority: priority,
                    executionMode: _state.EditExecutionMode,
                    status: _state.EditStatus);

                if (!string.IsNullOrWhiteSpace(_state.EditAssignedTo) ||
                    !string.IsNullOrWhiteSpace(_state.EditWorkingBranch) ||
                    tags.Count > 0 || linkedIssues.Count > 0 || parentIssues.Count > 0 ||
                    linkedPr.HasValue)
                {
                    await _issueService.UpdateIssueFullAsync(
                        newIssue.Id,
                        assignedTo: string.IsNullOrWhiteSpace(_state.EditAssignedTo) ? null : _state.EditAssignedTo.Trim(),
                        linkedPr: linkedPr,
                        linkedIssues: linkedIssues,
                        parentIssues: parentIssues,
                        tags: tags,
                        workingBranchId: string.IsNullOrWhiteSpace(_state.EditWorkingBranch) ? null : _state.EditWorkingBranch.Trim());
                }

                _state.EditingIssue = newIssue;
            }
            else
            {
                if (_state.EditingIssue == null)
                {
                    _state.StatusMessage = "No issue to update";
                    _state.HasPendingSaves = false;
                    return;
                }

                var updated = await _issueService.UpdateIssueFullAsync(
                    _state.EditingIssue.Id,
                    title: _state.EditTitle.Trim(),
                    description: string.IsNullOrWhiteSpace(_state.EditDescription) ? null : _state.EditDescription,
                    status: _state.EditStatus,
                    type: _state.EditType,
                    priority: priority,
                    assignedTo: string.IsNullOrWhiteSpace(_state.EditAssignedTo) ? null : _state.EditAssignedTo.Trim(),
                    linkedPr: linkedPr,
                    linkedIssues: linkedIssues,
                    parentIssues: parentIssues,
                    tags: tags,
                    workingBranchId: string.IsNullOrWhiteSpace(_state.EditWorkingBranch) ? null : _state.EditWorkingBranch.Trim(),
                    executionMode: _state.EditExecutionMode);

                if (updated != null)
                {
                    _state.EditingIssue = updated;
                }
            }

            await LoadIssuesAsync();
            _state.ApplyFilter();

            _state.HasPendingSaves = false;
            _state.StatusMessage = "Saved successfully";
            _state.CurrentView = _state.IsCreateMode ? ViewType.IssueList : ViewType.IssueDetail;
        }
        catch (Exception ex)
        {
            _state.EditError = $"Error saving: {ex.Message}";
            _state.StatusMessage = "Save failed";
            _state.HasPendingSaves = false;
        }
    }

    private async Task LoadIssuesAsync()
    {
        try
        {
            _state.StatusMessage = "Loading issues...";
            var issues = await _issueService.FilterAsync(includeTerminal: true);
            _state.Issues = issues;
            _state.StatusMessage = $"Loaded {issues.Count} issues";
        }
        catch (Exception ex)
        {
            _state.StatusMessage = $"Error loading issues: {ex.Message}";
        }
    }

    private async Task ReloadAndFilterAsync()
    {
        await LoadIssuesAsync();
        _state.ApplyFilter();
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..(maxLength - 1)] + "…";
    }
}
