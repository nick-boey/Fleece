using Fleece.Core.Models;
using Terminal.Gui;

namespace Fleece.Cli.Tui.Views;

/// <summary>
/// View displaying detailed information about a selected issue.
/// </summary>
public sealed class IssueDetailView : FrameView
{
    private readonly TextView _contentView;
    private Issue? _currentIssue;

    public IssueDetailView()
    {
        Title = "Details";
        BorderStyle = LineStyle.Single;

        _contentView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true
        };

        Add(_contentView);

        SetIssue(null);
    }

    /// <summary>
    /// Sets the issue to display.
    /// </summary>
    public void SetIssue(Issue? issue)
    {
        _currentIssue = issue;

        if (issue == null)
        {
            _contentView.Text = "No issue selected.\n\nSelect an issue from the list to view details.";
            Title = "Details";
            return;
        }

        Title = $"Details - {(issue.Id.Length > 7 ? issue.Id[..7] : issue.Id)}";
        _contentView.Text = FormatIssueDetails(issue);
        _contentView.MoveHome();
    }

    private static string FormatIssueDetails(Issue issue)
    {
        var lines = new List<string>
        {
            $"ID: {issue.Id}",
            $"Title: {issue.Title}",
            "",
            $"Status: {issue.Status}",
            $"Type: {issue.Type}"
        };

        if (issue.Priority.HasValue)
        {
            lines.Add($"Priority: {issue.Priority}");
        }

        if (!string.IsNullOrEmpty(issue.Group))
        {
            lines.Add($"Group: {issue.Group}");
        }

        if (!string.IsNullOrEmpty(issue.AssignedTo))
        {
            lines.Add($"Assigned To: {issue.AssignedTo}");
        }

        if (issue.Tags.Count > 0)
        {
            lines.Add($"Tags: {string.Join(", ", issue.Tags)}");
        }

        lines.Add("");

        if (!string.IsNullOrEmpty(issue.Description))
        {
            lines.Add("Description:");
            lines.Add(issue.Description);
            lines.Add("");
        }

        if (issue.LinkedPR.HasValue)
        {
            lines.Add($"Linked PR: #{issue.LinkedPR}");
        }

        if (issue.LinkedIssues.Count > 0)
        {
            lines.Add($"Linked Issues: {string.Join(", ", issue.LinkedIssues.Select(id => id.Length > 7 ? id[..7] : id))}");
        }

        if (issue.ParentIssues.Count > 0)
        {
            lines.Add($"Parent Issues: {string.Join(", ", issue.ParentIssues.Select(id => id.Length > 7 ? id[..7] : id))}");
        }

        if (issue.PreviousIssues.Count > 0)
        {
            lines.Add($"Previous Issues: {string.Join(", ", issue.PreviousIssues.Select(id => id.Length > 7 ? id[..7] : id))}");
        }

        if (!string.IsNullOrEmpty(issue.WorkingBranchId))
        {
            lines.Add($"Working Branch: {issue.WorkingBranchId}");
        }

        lines.Add("");
        lines.Add($"Created: {issue.CreatedAt:yyyy-MM-dd HH:mm}");

        if (!string.IsNullOrEmpty(issue.CreatedBy))
        {
            lines.Add($"Created By: {issue.CreatedBy}");
        }

        lines.Add($"Last Update: {issue.LastUpdate:yyyy-MM-dd HH:mm}");

        // Questions section
        if (issue.Questions.Count > 0)
        {
            lines.Add("");
            lines.Add("--- Questions ---");

            foreach (var question in issue.Questions)
            {
                lines.Add("");
                lines.Add($"[{question.Id}] {question.Text}");
                lines.Add($"  Asked by: {question.AskedBy ?? "unknown"} on {question.AskedAt:yyyy-MM-dd}");

                if (!string.IsNullOrEmpty(question.Answer))
                {
                    lines.Add($"  Answer: {question.Answer}");
                    lines.Add($"  Answered by: {question.AnsweredBy ?? "unknown"} on {question.AnsweredAt:yyyy-MM-dd}");
                }
                else
                {
                    lines.Add("  (Unanswered)");
                }
            }
        }

        return string.Join("\n", lines);
    }
}
