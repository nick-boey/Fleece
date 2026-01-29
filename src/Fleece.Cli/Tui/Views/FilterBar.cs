using System.Collections.ObjectModel;
using Fleece.Core.Models;
using Terminal.Gui;

namespace Fleece.Cli.Tui.Views;

/// <summary>
/// Filter bar for filtering issues by status, type, and priority.
/// </summary>
public sealed class FilterBar : View
{
    private readonly ComboBox _statusCombo;
    private readonly ComboBox _typeCombo;
    private readonly ComboBox _priorityCombo;

    private readonly ObservableCollection<string> _statusItems;
    private readonly ObservableCollection<string> _typeItems;
    private readonly ObservableCollection<string> _priorityItems;

    public event EventHandler? FilterChanged;

    public FilterBar()
    {
        // Status filter
        var statusLabel = new Label
        {
            X = 0,
            Y = 0,
            Text = "Status:"
        };

        _statusItems = new ObservableCollection<string>(["All", "Idea", "Spec", "Next", "Progress", "Review", "Complete"]);
        _statusCombo = new ComboBox
        {
            X = Pos.Right(statusLabel) + 1,
            Y = 0,
            Width = 12,
            Height = 1,
            ReadOnly = true,
            Source = new ListWrapper<string>(_statusItems),
            SelectedItem = 0
        };
        _statusCombo.SelectedItemChanged += (s, e) => FilterChanged?.Invoke(this, EventArgs.Empty);

        // Type filter
        var typeLabel = new Label
        {
            X = Pos.Right(_statusCombo) + 2,
            Y = 0,
            Text = "Type:"
        };

        _typeItems = new ObservableCollection<string>(["All", "Task", "Bug", "Chore", "Feature"]);
        _typeCombo = new ComboBox
        {
            X = Pos.Right(typeLabel) + 1,
            Y = 0,
            Width = 10,
            Height = 1,
            ReadOnly = true,
            Source = new ListWrapper<string>(_typeItems),
            SelectedItem = 0
        };
        _typeCombo.SelectedItemChanged += (s, e) => FilterChanged?.Invoke(this, EventArgs.Empty);

        // Priority filter
        var priorityLabel = new Label
        {
            X = Pos.Right(_typeCombo) + 2,
            Y = 0,
            Text = "Priority:"
        };

        _priorityItems = new ObservableCollection<string>(["All", "1", "2", "3", "4", "5"]);
        _priorityCombo = new ComboBox
        {
            X = Pos.Right(priorityLabel) + 1,
            Y = 0,
            Width = 6,
            Height = 1,
            ReadOnly = true,
            Source = new ListWrapper<string>(_priorityItems),
            SelectedItem = 0
        };
        _priorityCombo.SelectedItemChanged += (s, e) => FilterChanged?.Invoke(this, EventArgs.Empty);

        Add(statusLabel, _statusCombo, typeLabel, _typeCombo, priorityLabel, _priorityCombo);
    }

    /// <summary>
    /// Gets the current filter values.
    /// </summary>
    public (IssueStatus? Status, IssueType? Type, int? Priority) GetFilters()
    {
        IssueStatus? status = _statusCombo.SelectedItem > 0
            ? Enum.Parse<IssueStatus>(_statusItems[_statusCombo.SelectedItem])
            : null;

        IssueType? type = _typeCombo.SelectedItem > 0
            ? Enum.Parse<IssueType>(_typeItems[_typeCombo.SelectedItem])
            : null;

        int? priority = _priorityCombo.SelectedItem > 0
            ? int.Parse(_priorityItems[_priorityCombo.SelectedItem])
            : null;

        return (status, type, priority);
    }

    /// <summary>
    /// Focus the first filter control.
    /// </summary>
    public void FocusFirstFilter()
    {
        _statusCombo.SetFocus();
    }
}
