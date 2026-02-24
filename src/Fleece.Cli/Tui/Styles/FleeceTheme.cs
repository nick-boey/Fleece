using Fleece.Core.Models;
using XenoAtom.Ansi;
using XenoAtom.Terminal.UI;

namespace Fleece.Cli.Tui.Styles;

/// <summary>
/// Color theme constants for the Fleece TUI.
/// </summary>
public static class FleeceTheme
{
    // Status colors (matching existing TUI)
    public static readonly Style StatusDraft = Style.None.WithForeground(AnsiColors.BrightBlack);
    public static readonly Style StatusOpen = Style.None.WithForeground(AnsiColors.Cyan);
    public static readonly Style StatusProgress = Style.None.WithForeground(AnsiColors.Yellow);
    public static readonly Style StatusReview = Style.None.WithForeground(AnsiColors.Magenta);
    public static readonly Style StatusComplete = Style.None.WithForeground(AnsiColors.Green);
    public static readonly Style StatusArchived = Style.None.WithForeground(AnsiColors.BrightBlack);
    public static readonly Style StatusClosed = Style.None.WithForeground(AnsiColors.Green);
    public static readonly Style StatusDeleted = Style.None.WithForeground(AnsiColors.Red);

    // Type colors
    public static readonly Style TypeTask = Style.None.WithForeground(AnsiColors.Blue);
    public static readonly Style TypeBug = Style.None.WithForeground(AnsiColors.Red);
    public static readonly Style TypeChore = Style.None.WithForeground(AnsiColors.BrightBlack);
    public static readonly Style TypeFeature = Style.None.WithForeground(AnsiColors.Magenta);

    // Priority colors
    public static readonly Style PriorityHigh = Style.None.WithForeground(AnsiColors.Red);
    public static readonly Style PriorityMedium = Style.None.WithForeground(AnsiColors.Yellow);
    public static readonly Style PriorityLow = Style.None.WithForeground(AnsiColors.Green);

    // UI element colors
    public static readonly Style HeaderStyle = Style.None.WithForeground(AnsiColors.White).WithBackground(AnsiColors.Blue);
    public static readonly Style FooterStyle = Style.None.WithForeground(AnsiColors.White).WithBackground(AnsiColors.BrightBlack);
    public static readonly Style SelectedStyle = Style.None.WithForeground(AnsiColors.White).WithBackground(AnsiColors.Cyan);
    public static readonly Style LabelStyle = Style.None.WithForeground(AnsiColors.BrightBlack);
    public static readonly Style ValueStyle = Style.None.WithForeground(AnsiColors.White);
    public static readonly Style ErrorStyle = Style.None.WithForeground(AnsiColors.Red);
    public static readonly Style SuccessStyle = Style.None.WithForeground(AnsiColors.Green);

    /// <summary>
    /// Gets the style for a given issue status.
    /// </summary>
    public static Style GetStatusStyle(IssueStatus status) => status switch
    {
        IssueStatus.Draft => StatusDraft,
        IssueStatus.Open => StatusOpen,
        IssueStatus.Progress => StatusProgress,
        IssueStatus.Review => StatusReview,
        IssueStatus.Complete => StatusComplete,
        IssueStatus.Archived => StatusArchived,
        IssueStatus.Closed => StatusClosed,
        IssueStatus.Deleted => StatusDeleted,
        _ => Style.None
    };

    /// <summary>
    /// Gets the style for a given issue type.
    /// </summary>
    public static Style GetTypeStyle(IssueType type) => type switch
    {
        IssueType.Task => TypeTask,
        IssueType.Bug => TypeBug,
        IssueType.Chore => TypeChore,
        IssueType.Feature => TypeFeature,
        _ => Style.None
    };

    /// <summary>
    /// Gets the style for a given priority level.
    /// </summary>
    public static Style GetPriorityStyle(int? priority) => priority switch
    {
        1 => PriorityHigh,
        2 => PriorityHigh,
        3 => PriorityMedium,
        4 => PriorityLow,
        5 => PriorityLow,
        _ => Style.None
    };
}
