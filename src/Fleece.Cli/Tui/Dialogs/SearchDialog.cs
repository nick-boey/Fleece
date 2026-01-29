using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Terminal.Gui;

namespace Fleece.Cli.Tui.Dialogs;

/// <summary>
/// Dialog for searching issues.
/// </summary>
public sealed class SearchDialog : Dialog
{
    private readonly IIssueService _issueService;
    private readonly TextField _searchField;
    private readonly TableView _resultsTable;
    private readonly Label _countLabel;
    private IReadOnlyList<Issue> _searchResults = [];
    private System.Data.DataTable _dataTable;

    /// <summary>
    /// Gets the selected issue from search results.
    /// </summary>
    public Issue? SelectedIssue { get; private set; }

    public SearchDialog(IIssueService issueService)
    {
        _issueService = issueService;

        Title = "Search Issues";
        Width = Dim.Percent(80);
        Height = Dim.Percent(70);

        // Search field
        var searchLabel = new Label { X = 1, Y = 0, Text = "Search:" };
        _searchField = new TextField
        {
            X = 10,
            Y = 0,
            Width = Dim.Fill(12)
        };

        var searchButton = new Button
        {
            X = Pos.Right(_searchField) + 1,
            Y = 0,
            Text = "Go"
        };
        searchButton.Accepting += OnSearch;

        // Handle Enter key in search field
        _searchField.KeyDown += (s, e) =>
        {
            if (e.KeyCode == KeyCode.Enter)
            {
                PerformSearch();
                e.Handled = true;
            }
        };

        // Results table
        _dataTable = CreateDataTable();
        _resultsTable = new TableView
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(1),
            Height = Dim.Fill(3),
            FullRowSelect = true
        };
        _resultsTable.Table = new DataTableSource(_dataTable);
        _resultsTable.Style.ShowHeaders = true;
        _resultsTable.Style.ShowHorizontalHeaderUnderline = true;
        _resultsTable.Style.ShowVerticalCellLines = true;

        // Handle double-click or Enter on result
        _resultsTable.KeyDown += (s, e) =>
        {
            if (e.KeyCode == KeyCode.Enter)
            {
                SelectCurrentResult();
                e.Handled = true;
            }
        };

        // Count label
        _countLabel = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(2),
            Text = "Enter a search term and press Enter or click Go"
        };

        // Buttons
        var selectButton = new Button
        {
            Text = "Select",
            IsDefault = true
        };
        selectButton.Accepting += (s, e) => SelectCurrentResult();

        var cancelButton = new Button
        {
            Text = "Cancel"
        };
        cancelButton.Accepting += (s, e) => RequestStop();

        Add(searchLabel, _searchField, searchButton, _resultsTable, _countLabel);
        AddButton(cancelButton);
        AddButton(selectButton);

        _searchField.SetFocus();
    }

    private static System.Data.DataTable CreateDataTable()
    {
        var table = new System.Data.DataTable();
        table.Columns.Add("ID", typeof(string));
        table.Columns.Add("Title", typeof(string));
        table.Columns.Add("Type", typeof(string));
        table.Columns.Add("Status", typeof(string));
        return table;
    }

    private void OnSearch(object? sender, CommandEventArgs e)
    {
        PerformSearch();
    }

    private void PerformSearch()
    {
        var query = _searchField.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(query))
        {
            _countLabel.Text = "Please enter a search term";
            return;
        }

        try
        {
            _searchResults = _issueService.SearchAsync(query).GetAwaiter().GetResult();

            _dataTable.Rows.Clear();

            foreach (var issue in _searchResults)
            {
                var shortId = issue.Id.Length > 7 ? issue.Id[..7] : issue.Id;
                var title = issue.Title.Length > 50 ? issue.Title[..47] + "..." : issue.Title;

                _dataTable.Rows.Add(shortId, title, issue.Type.ToString(), issue.Status.ToString());
            }

            _resultsTable.Table = new DataTableSource(_dataTable);

            _countLabel.Text = $"Found {_searchResults.Count} result(s)";

            if (_searchResults.Count > 0)
            {
                _resultsTable.SelectedRow = 0;
            }
        }
        catch (Exception ex)
        {
            _countLabel.Text = $"Error: {ex.Message}";
        }
    }

    private void SelectCurrentResult()
    {
        if (_resultsTable.SelectedRow >= 0 && _resultsTable.SelectedRow < _searchResults.Count)
        {
            SelectedIssue = _searchResults[_resultsTable.SelectedRow];
            RequestStop();
        }
    }
}
