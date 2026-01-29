using System.Collections.ObjectModel;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Terminal.Gui;

namespace Fleece.Cli.Tui.Dialogs;

/// <summary>
/// Dialog for creating a new issue.
/// </summary>
public sealed class CreateIssueDialog : Dialog
{
    private readonly IIssueService _issueService;

    private readonly TextField _titleField;
    private readonly ComboBox _typeCombo;
    private readonly ComboBox _statusCombo;
    private readonly TextField _priorityField;
    private readonly TextField _groupField;
    private readonly TextField _assignedField;
    private readonly TextField _tagsField;
    private readonly TextView _descriptionView;

    private readonly ObservableCollection<string> _typeItems;
    private readonly ObservableCollection<string> _statusItems;

    /// <summary>
    /// Gets whether an issue was successfully created.
    /// </summary>
    public bool WasCreated { get; private set; }

    public CreateIssueDialog(IIssueService issueService)
    {
        _issueService = issueService;

        Title = "Create New Issue";
        Width = Dim.Percent(80);
        Height = Dim.Percent(80);

        var y = 0;

        // Title
        var titleLabel = new Label { X = 1, Y = y, Text = "Title:" };
        _titleField = new TextField
        {
            X = 12,
            Y = y,
            Width = Dim.Fill(2)
        };
        y += 2;

        // Type
        var typeLabel = new Label { X = 1, Y = y, Text = "Type:" };
        _typeItems = new ObservableCollection<string>(Enum.GetNames<IssueType>());
        _typeCombo = new ComboBox
        {
            X = 12,
            Y = y,
            Width = 15,
            Height = 1,
            ReadOnly = true,
            Source = new ListWrapper<string>(_typeItems),
            SelectedItem = 0 // Task
        };

        // Status (on same row)
        var statusLabel = new Label { X = 30, Y = y, Text = "Status:" };
        _statusItems = new ObservableCollection<string>(["Idea", "Spec", "Next", "Progress"]);
        _statusCombo = new ComboBox
        {
            X = 40,
            Y = y,
            Width = 15,
            Height = 1,
            ReadOnly = true,
            Source = new ListWrapper<string>(_statusItems),
            SelectedItem = 0 // Idea
        };
        y += 2;

        // Priority
        var priorityLabel = new Label { X = 1, Y = y, Text = "Priority:" };
        _priorityField = new TextField
        {
            X = 12,
            Y = y,
            Width = 5,
            Text = ""
        };
        var priorityHint = new Label { X = 18, Y = y, Text = "(1-5, optional)" };

        // Group (on same row)
        var groupLabel = new Label { X = 35, Y = y, Text = "Group:" };
        _groupField = new TextField
        {
            X = 43,
            Y = y,
            Width = Dim.Fill(2)
        };
        y += 2;

        // Assigned To
        var assignedLabel = new Label { X = 1, Y = y, Text = "Assigned:" };
        _assignedField = new TextField
        {
            X = 12,
            Y = y,
            Width = 25
        };

        // Tags (on same row)
        var tagsLabel = new Label { X = 40, Y = y, Text = "Tags:" };
        _tagsField = new TextField
        {
            X = 47,
            Y = y,
            Width = Dim.Fill(2),
            Text = ""
        };
        y += 2;

        // Description
        var descLabel = new Label { X = 1, Y = y, Text = "Description:" };
        y++;

        var descFrame = new FrameView
        {
            X = 1,
            Y = y,
            Width = Dim.Fill(2),
            Height = Dim.Fill(3),
            BorderStyle = LineStyle.Single
        };

        _descriptionView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            WordWrap = true
        };
        descFrame.Add(_descriptionView);

        // Buttons
        var createButton = new Button
        {
            Text = "Create",
            IsDefault = true
        };
        createButton.Accepting += OnCreate;

        var cancelButton = new Button
        {
            Text = "Cancel"
        };
        cancelButton.Accepting += (s, e) => RequestStop();

        Add(
            titleLabel, _titleField,
            typeLabel, _typeCombo, statusLabel, _statusCombo,
            priorityLabel, _priorityField, priorityHint, groupLabel, _groupField,
            assignedLabel, _assignedField, tagsLabel, _tagsField,
            descLabel, descFrame
        );

        AddButton(cancelButton);
        AddButton(createButton);

        _titleField.SetFocus();
    }

    private void OnCreate(object? sender, CommandEventArgs e)
    {
        // Validate title
        var title = _titleField.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(title))
        {
            MessageBox.ErrorQuery("Validation Error", "Title is required.", "OK");
            return;
        }

        // Parse values
        var type = Enum.Parse<IssueType>(_typeItems[_typeCombo.SelectedItem]);
        var status = Enum.Parse<IssueStatus>(_statusItems[_statusCombo.SelectedItem]);

        int? priority = null;
        var priorityText = _priorityField.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(priorityText))
        {
            if (int.TryParse(priorityText, out var p) && p >= 1 && p <= 5)
            {
                priority = p;
            }
            else
            {
                MessageBox.ErrorQuery("Validation Error", "Priority must be a number between 1 and 5.", "OK");
                return;
            }
        }

        var group = string.IsNullOrWhiteSpace(_groupField.Text) ? null : _groupField.Text.Trim();
        var assignedTo = string.IsNullOrWhiteSpace(_assignedField.Text) ? null : _assignedField.Text.Trim();
        var description = string.IsNullOrWhiteSpace(_descriptionView.Text) ? null : _descriptionView.Text.Trim();

        IReadOnlyList<string>? tags = null;
        var tagsText = _tagsField.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(tagsText))
        {
            tags = tagsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        try
        {
            // Create the issue
            _issueService.CreateAsync(
                title: title,
                type: type,
                description: description,
                status: status,
                priority: priority,
                group: group,
                assignedTo: assignedTo,
                tags: tags
            ).GetAwaiter().GetResult();

            WasCreated = true;
            RequestStop();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to create issue: {ex.Message}", "OK");
        }
    }
}
