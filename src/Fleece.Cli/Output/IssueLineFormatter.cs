using Fleece.Core.Models;
using Spectre.Console;

namespace Fleece.Cli.Output;

/// <summary>
/// Consolidated one-line issue formatting shared across show, tree, and list commands.
/// </summary>
public static class IssueLineFormatter
{
    /// <summary>
    /// Returns Spectre.Console markup for a single issue line:
    /// {id} [{type}] [{status}] P{priority} {title}
    /// </summary>
    public static string FormatMarkup(Issue issue)
    {
        var statusColor = GetStatusColor(issue.Status);
        var typeLabel = issue.Type.ToString().ToLowerInvariant();
        var statusLabel = issue.Status.ToString().ToLowerInvariant();
        var priorityStr = issue.Priority.HasValue ? $"[dim]P{issue.Priority}[/] " : "";
        var typeTag = Markup.Escape($"[{typeLabel}]");
        var statusTag = Markup.Escape($"[{statusLabel}]");

        return $"[{statusColor}]{issue.Id}[/] [{statusColor}]{typeTag}[/] [{statusColor}]{statusTag}[/] {priorityStr}{Markup.Escape(issue.Title)}";
    }

    /// <summary>
    /// Returns Spectre.Console markup for an IssueSummaryDto:
    /// {id} [{type}] [{status}] P{priority} {title}
    /// </summary>
    public static string FormatMarkup(IssueSummaryDto summary)
    {
        var statusColor = GetStatusColor(summary.Status);
        var typeLabel = summary.Type.ToString().ToLowerInvariant();
        var statusLabel = summary.Status.ToString().ToLowerInvariant();
        var priorityStr = summary.Priority.HasValue ? $"[dim]P{summary.Priority}[/] " : "";
        var typeTag = Markup.Escape($"[{typeLabel}]");
        var statusTag = Markup.Escape($"[{statusLabel}]");

        return $"[{statusColor}]{summary.Id}[/] [{statusColor}]{typeTag}[/] [{statusColor}]{statusTag}[/] {priorityStr}{Markup.Escape(summary.Title)}";
    }

    /// <summary>
    /// Returns plain text for a single issue line:
    /// {id} {status} {type} {title}
    /// </summary>
    public static string FormatPlainText(Issue issue)
    {
        return $"{issue.Id} {issue.Status.ToString().ToLowerInvariant()} {issue.Type.ToString().ToLowerInvariant()} {issue.Title}";
    }

    /// <summary>
    /// Returns the Spectre.Console color name for a given issue status.
    /// </summary>
    public static string GetStatusColor(IssueStatus status) => status switch
    {
        IssueStatus.Open => "cyan",
        IssueStatus.Progress => "blue",
        IssueStatus.Review => "purple",
        IssueStatus.Complete => "green",
        IssueStatus.Archived => "dim",
        IssueStatus.Closed => "dim",
        _ => "white"
    };
}
