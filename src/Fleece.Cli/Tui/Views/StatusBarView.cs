using Terminal.Gui;

namespace Fleece.Cli.Tui.Views;

/// <summary>
/// Status bar showing keyboard shortcuts and issue count.
/// </summary>
public sealed class StatusBarView : View
{
    private readonly Label _shortcutsLabel;
    private readonly Label _countLabel;

    public StatusBarView()
    {
        ColorScheme = TuiColors.MenuScheme;

        _shortcutsLabel = new Label
        {
            X = 0,
            Y = 0,
            Text = " [N]ew  [E]dit  [D]elete  [Q]uestion  [/]Search  [F]ilter  [R]efresh  [?]Help  Ctrl+Q:Quit "
        };

        _countLabel = new Label
        {
            X = Pos.AnchorEnd(20),
            Y = 0,
            Width = 20,
            TextAlignment = Alignment.End
        };

        Add(_shortcutsLabel, _countLabel);
    }

    /// <summary>
    /// Sets the issue count to display.
    /// </summary>
    public void SetIssueCount(int count)
    {
        _countLabel.Text = $"{count} issue(s)";
    }
}
