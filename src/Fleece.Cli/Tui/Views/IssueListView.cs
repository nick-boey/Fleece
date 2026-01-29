using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Terminal.Gui;

namespace Fleece.Cli.Tui.Views;

/// <summary>
/// View displaying a list of issues in a table format.
/// </summary>
public sealed class IssueListView : FrameView
{
    private readonly IIssueService _issueService;
    private readonly TableView _tableView;
    private IReadOnlyList<Issue> _issues = [];
    private System.Data.DataTable _dataTable;

    public event EventHandler<Issue?>? SelectionChanged;
    public event EventHandler<Issue>? IssueActivated;

    /// <summary>
    /// Gets the currently selected issue.
    /// </summary>
    public Issue? SelectedIssue
    {
        get
        {
            if (_tableView.SelectedRow >= 0 && _tableView.SelectedRow < _issues.Count)
            {
                return _issues[_tableView.SelectedRow];
            }
            return null;
        }
    }

    public IssueListView(IIssueService issueService)
    {
        _issueService = issueService;
        Title = "Issues";
        BorderStyle = LineStyle.Single;

        _dataTable = CreateDataTable();

        _tableView = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            FullRowSelect = true
        };

        _tableView.Table = new DataTableSource(_dataTable);
        _tableView.Style.ShowHeaders = true;
        _tableView.Style.ShowHorizontalHeaderOverline = false;
        _tableView.Style.ShowHorizontalHeaderUnderline = true;
        _tableView.Style.ShowVerticalCellLines = true;

        // Handle selection changes
        _tableView.SelectedCellChanged += OnSelectedCellChanged;

        // Handle Enter key to activate issue
        _tableView.KeyDown += (s, e) =>
        {
            if (e.KeyCode == KeyCode.Enter)
            {
                var issue = SelectedIssue;
                if (issue != null)
                {
                    IssueActivated?.Invoke(this, issue);
                    e.Handled = true;
                }
            }
            // Vim-style navigation
            else if (e.KeyCode == KeyCode.J)
            {
                if (_tableView.SelectedRow < _issues.Count - 1)
                {
                    _tableView.SelectedRow++;
                }
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.K)
            {
                if (_tableView.SelectedRow > 0)
                {
                    _tableView.SelectedRow--;
                }
                e.Handled = true;
            }
        };

        Add(_tableView);
    }

    private static System.Data.DataTable CreateDataTable()
    {
        var table = new System.Data.DataTable();
        table.Columns.Add("ID", typeof(string));
        table.Columns.Add("Title", typeof(string));
        table.Columns.Add("Type", typeof(string));
        table.Columns.Add("Status", typeof(string));
        table.Columns.Add("Pri", typeof(string));
        return table;
    }

    /// <summary>
    /// Sets the issues to display.
    /// </summary>
    public void SetIssues(IReadOnlyList<Issue> issues)
    {
        _issues = issues.OrderByDescending(i => i.LastUpdate).ToList();

        _dataTable.Rows.Clear();

        foreach (var issue in _issues)
        {
            var shortId = issue.Id.Length > 7 ? issue.Id[..7] : issue.Id;
            var title = issue.Title.Length > 40 ? issue.Title[..37] + "..." : issue.Title;
            var priority = issue.Priority?.ToString() ?? "-";

            _dataTable.Rows.Add(shortId, title, issue.Type.ToString(), issue.Status.ToString(), priority);
        }

        _tableView.Table = new DataTableSource(_dataTable);

        // Trigger selection changed for initial selection
        if (_issues.Count > 0)
        {
            _tableView.SelectedRow = 0;
            SelectionChanged?.Invoke(this, SelectedIssue);
        }
        else
        {
            SelectionChanged?.Invoke(this, null);
        }
    }

    /// <summary>
    /// Selects an issue by ID.
    /// </summary>
    public void SelectIssue(Issue issue)
    {
        var index = _issues.ToList().FindIndex(i => i.Id == issue.Id);
        if (index >= 0)
        {
            _tableView.SelectedRow = index;
            _tableView.EnsureSelectedCellIsVisible();
            SelectionChanged?.Invoke(this, SelectedIssue);
        }
    }

    private void OnSelectedCellChanged(object? sender, SelectedCellChangedEventArgs e)
    {
        SelectionChanged?.Invoke(this, SelectedIssue);
    }
}
