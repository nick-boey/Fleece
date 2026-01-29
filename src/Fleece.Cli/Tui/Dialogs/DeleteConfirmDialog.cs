using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Terminal.Gui;

namespace Fleece.Cli.Tui.Dialogs;

/// <summary>
/// Dialog for confirming issue deletion.
/// </summary>
public sealed class DeleteConfirmDialog : Dialog
{
    private readonly IIssueService _issueService;
    private readonly Issue _issue;

    /// <summary>
    /// Gets whether the issue was successfully deleted.
    /// </summary>
    public bool WasDeleted { get; private set; }

    public DeleteConfirmDialog(IIssueService issueService, Issue issue)
    {
        _issueService = issueService;
        _issue = issue;

        Title = "Confirm Delete";
        Width = 60;
        Height = 10;

        var shortId = issue.Id.Length > 7 ? issue.Id[..7] : issue.Id;
        var titlePreview = issue.Title.Length > 30 ? issue.Title[..27] + "..." : issue.Title;

        var messageLabel = new Label
        {
            X = Pos.Center(),
            Y = 1,
            Text = "Are you sure you want to delete this issue?"
        };

        var issueLabel = new Label
        {
            X = Pos.Center(),
            Y = 3,
            Text = $"\"{shortId} - {titlePreview}\""
        };

        var warningLabel = new Label
        {
            X = Pos.Center(),
            Y = 5,
            Text = "(Issue will be marked as Deleted)"
        };

        // Buttons
        var deleteButton = new Button
        {
            Text = "Delete"
        };
        deleteButton.Accepting += OnDelete;

        var cancelButton = new Button
        {
            Text = "Cancel",
            IsDefault = true
        };
        cancelButton.Accepting += (s, e) => RequestStop();

        Add(messageLabel, issueLabel, warningLabel);
        AddButton(cancelButton);
        AddButton(deleteButton);
    }

    private void OnDelete(object? sender, CommandEventArgs e)
    {
        try
        {
            var result = _issueService.DeleteAsync(_issue.Id).GetAwaiter().GetResult();

            if (result)
            {
                WasDeleted = true;
            }
            else
            {
                MessageBox.ErrorQuery("Error", "Issue not found.", "OK");
            }

            RequestStop();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to delete issue: {ex.Message}", "OK");
        }
    }
}
