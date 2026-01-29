using Fleece.Core.Models;
using Terminal.Gui;

namespace Fleece.Cli.Tui;

/// <summary>
/// Color scheme for the TUI, matching the existing TableFormatter colors.
/// </summary>
public static class TuiColors
{
    /// <summary>
    /// Gets the color attribute for an issue status.
    /// </summary>
    public static Color GetStatusColor(IssueStatus status) => status switch
    {
        IssueStatus.Idea => Color.Magenta,
        IssueStatus.Spec => Color.Cyan,
        IssueStatus.Next => Color.Yellow,
        IssueStatus.Progress => Color.Blue,
        IssueStatus.Review => Color.Magenta,
        IssueStatus.Complete => Color.Green,
        IssueStatus.Archived => Color.Gray,
        IssueStatus.Closed => Color.Gray,
        IssueStatus.Deleted => Color.DarkGray,
        _ => Color.White
    };

    /// <summary>
    /// Gets the color attribute for an issue type.
    /// </summary>
    public static Color GetTypeColor(IssueType type) => type switch
    {
        IssueType.Bug => Color.Red,
        IssueType.Feature => Color.Cyan,
        IssueType.Task => Color.White,
        IssueType.Chore => Color.Gray,
        _ => Color.White
    };

    /// <summary>
    /// Gets the default application color scheme.
    /// </summary>
    public static Terminal.Gui.ColorScheme DefaultScheme => new()
    {
        Normal = new Terminal.Gui.Attribute(Color.White, Color.Blue),
        Focus = new Terminal.Gui.Attribute(Color.Black, Color.Cyan),
        HotNormal = new Terminal.Gui.Attribute(Color.Yellow, Color.Blue),
        HotFocus = new Terminal.Gui.Attribute(Color.Yellow, Color.Cyan),
        Disabled = new Terminal.Gui.Attribute(Color.Gray, Color.Blue)
    };

    /// <summary>
    /// Gets the color scheme for dialogs.
    /// </summary>
    public static Terminal.Gui.ColorScheme DialogScheme => new()
    {
        Normal = new Terminal.Gui.Attribute(Color.Black, Color.Gray),
        Focus = new Terminal.Gui.Attribute(Color.Black, Color.Cyan),
        HotNormal = new Terminal.Gui.Attribute(Color.Blue, Color.Gray),
        HotFocus = new Terminal.Gui.Attribute(Color.Blue, Color.Cyan),
        Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Gray)
    };

    /// <summary>
    /// Gets the color scheme for the menu/status bar.
    /// </summary>
    public static Terminal.Gui.ColorScheme MenuScheme => new()
    {
        Normal = new Terminal.Gui.Attribute(Color.White, Color.DarkGray),
        Focus = new Terminal.Gui.Attribute(Color.White, Color.Black),
        HotNormal = new Terminal.Gui.Attribute(Color.Yellow, Color.DarkGray),
        HotFocus = new Terminal.Gui.Attribute(Color.Yellow, Color.Black),
        Disabled = new Terminal.Gui.Attribute(Color.Gray, Color.DarkGray)
    };
}
