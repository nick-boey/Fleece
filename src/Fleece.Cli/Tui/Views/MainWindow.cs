using System.Collections.ObjectModel;
using Fleece.Cli.Tui.Dialogs;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;

namespace Fleece.Cli.Tui.Views;

/// <summary>
/// Main window containing the TUI layout.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly IIssueService _issueService;

    private readonly FilterBar _filterBar;
    private readonly IssueListView _issueListView;
    private readonly IssueDetailView _issueDetailView;
    private readonly StatusBarView _statusBar;

    public MainWindow(IServiceProvider services)
    {
        _services = services;
        _issueService = services.GetRequiredService<IIssueService>();

        Title = "Fleece Issue Tracker";
        ColorScheme = TuiColors.DefaultScheme;

        // Create filter bar at the top
        _filterBar = new FilterBar
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1
        };
        _filterBar.FilterChanged += OnFilterChanged;

        // Create main content area with horizontal split
        var contentFrame = new View
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1) // Leave room for status bar
        };

        // Issue list on the left (60%)
        _issueListView = new IssueListView(_issueService)
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(60),
            Height = Dim.Fill()
        };
        _issueListView.SelectionChanged += OnIssueSelectionChanged;
        _issueListView.IssueActivated += OnIssueActivated;

        // Issue detail on the right (40%)
        _issueDetailView = new IssueDetailView
        {
            X = Pos.Right(_issueListView),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        contentFrame.Add(_issueListView, _issueDetailView);

        // Status bar at the bottom
        _statusBar = new StatusBarView
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1
        };

        Add(_filterBar, contentFrame, _statusBar);

        // Set up keyboard shortcuts
        SetupKeyBindings();

        // Load initial data
        LoadIssuesAsync().ConfigureAwait(false);
    }

    private void SetupKeyBindings()
    {
        // Global keyboard shortcuts
        KeyDown += (s, e) =>
        {
            switch (e.KeyCode)
            {
                case KeyCode.N:
                    CreateNewIssue();
                    e.Handled = true;
                    break;
                case KeyCode.E:
                    EditSelectedIssue();
                    e.Handled = true;
                    break;
                case KeyCode.D:
                    DeleteSelectedIssue();
                    e.Handled = true;
                    break;
                case KeyCode.Q when !e.KeyCode.HasFlag(KeyCode.CtrlMask):
                    AddQuestion();
                    e.Handled = true;
                    break;
                case (KeyCode)'/' :
                    ShowSearchDialog();
                    e.Handled = true;
                    break;
                case KeyCode.F:
                    FocusFilter();
                    e.Handled = true;
                    break;
                case KeyCode.R:
                    _ = RefreshAsync();
                    e.Handled = true;
                    break;
                case KeyCode.ShiftMask | (KeyCode)'?':
                    ShowHelp();
                    e.Handled = true;
                    break;
                case KeyCode.Q | KeyCode.CtrlMask:
                case KeyCode.Esc:
                    RequestStop();
                    e.Handled = true;
                    break;
            }
        };
    }

    private async Task LoadIssuesAsync()
    {
        try
        {
            var filters = _filterBar.GetFilters();
            var issues = await _issueService.FilterAsync(
                status: filters.Status,
                type: filters.Type,
                priority: filters.Priority,
                includeTerminal: false
            );

            Application.Invoke(() =>
            {
                _issueListView.SetIssues(issues);
                _statusBar.SetIssueCount(issues.Count);
            });
        }
        catch (Exception ex)
        {
            Application.Invoke(() =>
            {
                MessageBox.ErrorQuery("Error", $"Failed to load issues: {ex.Message}", "OK");
            });
        }
    }

    private async Task RefreshAsync()
    {
        await LoadIssuesAsync();
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        _ = LoadIssuesAsync();
    }

    private void OnIssueSelectionChanged(object? sender, Issue? issue)
    {
        _issueDetailView.SetIssue(issue);
    }

    private void OnIssueActivated(object? sender, Issue issue)
    {
        EditIssue(issue);
    }

    private void CreateNewIssue()
    {
        var dialog = new CreateIssueDialog(_issueService);
        Application.Run(dialog);

        if (dialog.WasCreated)
        {
            _ = LoadIssuesAsync();
        }

        dialog.Dispose();
    }

    private void EditSelectedIssue()
    {
        var issue = _issueListView.SelectedIssue;
        if (issue != null)
        {
            EditIssue(issue);
        }
    }

    private void EditIssue(Issue issue)
    {
        var dialog = new EditIssueDialog(_issueService, issue);
        Application.Run(dialog);

        if (dialog.WasSaved)
        {
            _ = LoadIssuesAsync();
        }

        dialog.Dispose();
    }

    private void DeleteSelectedIssue()
    {
        var issue = _issueListView.SelectedIssue;
        if (issue == null)
        {
            return;
        }

        var dialog = new DeleteConfirmDialog(_issueService, issue);
        Application.Run(dialog);

        if (dialog.WasDeleted)
        {
            _ = LoadIssuesAsync();
        }

        dialog.Dispose();
    }

    private void AddQuestion()
    {
        var issue = _issueListView.SelectedIssue;
        if (issue == null)
        {
            return;
        }

        var dialog = new QuestionDialog(_issueService, issue);
        Application.Run(dialog);

        if (dialog.WasSaved)
        {
            _ = LoadIssuesAsync();
        }

        dialog.Dispose();
    }

    private void ShowSearchDialog()
    {
        var dialog = new SearchDialog(_issueService);
        Application.Run(dialog);

        if (dialog.SelectedIssue != null)
        {
            _issueListView.SelectIssue(dialog.SelectedIssue);
        }

        dialog.Dispose();
    }

    private void FocusFilter()
    {
        _filterBar.FocusFirstFilter();
    }

    private void ShowHelp()
    {
        var helpText = """
            Keyboard Shortcuts:

            n       - Create new issue
            e       - Edit selected issue
            d       - Delete selected issue
            q       - Add question to issue
            /       - Search issues
            f       - Focus filter bar
            r       - Refresh issue list
            ?       - Show this help

            Enter   - Edit selected issue
            Tab     - Move between panels
            Ctrl+Q  - Quit
            Esc     - Quit

            Navigation:
            Up/Down - Move selection
            j/k     - Move selection (vim-style)
            """;

        MessageBox.Query("Help", helpText, "OK");
    }
}
