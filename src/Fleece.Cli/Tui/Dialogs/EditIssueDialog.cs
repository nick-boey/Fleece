using System.Collections.ObjectModel;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Terminal.Gui;

namespace Fleece.Cli.Tui.Dialogs;

/// <summary>
/// Dialog for editing an existing issue.
/// </summary>
public sealed class EditIssueDialog : Dialog
{
    private readonly IIssueService _issueService;
    private readonly Issue _issue;

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
    /// Gets whether the issue was successfully saved.
    /// </summary>
    public bool WasSaved { get; private set; }

    public EditIssueDialog(IIssueService issueService, Issue issue)
    {
        _issueService = issueService;
        _issue = issue;

        var shortId = issue.Id.Length > 7 ? issue.Id[..7] : issue.Id;
        Title = $"Edit Issue - {shortId}";
        Width = Dim.Percent(80);
        Height = Dim.Percent(80);

        var y = 0;

        // Title
        var titleLabel = new Label { X = 1, Y = y, Text = "Title:" };
        _titleField = new TextField
        {
            X = 12,
            Y = y,
            Width = Dim.Fill(2),
            Text = issue.Title
        };
        y += 2;

        // Type
        var typeLabel = new Label { X = 1, Y = y, Text = "Type:" };
        var typeNames = Enum.GetNames<IssueType>();
        _typeItems = new ObservableCollection<string>(typeNames);
        _typeCombo = new ComboBox
        {
            X = 12,
            Y = y,
            Width = 15,
            Height = 1,
            ReadOnly = true,
            Source = new ListWrapper<string>(_typeItems),
            SelectedItem = Array.IndexOf(typeNames, issue.Type.ToString())
        };

        // Status (on same row)
        var statusLabel = new Label { X = 30, Y = y, Text = "Status:" };
        var statusNames = Enum.GetNames<IssueStatus>().Where(s => s != "Deleted").ToArray();
        _statusItems = new ObservableCollection<string>(statusNames);
        _statusCombo = new ComboBox
        {
            X = 40,
            Y = y,
            Width = 15,
            Height = 1,
            ReadOnly = true,
            Source = new ListWrapper<string>(_statusItems),
            SelectedItem = Array.IndexOf(statusNames, issue.Status.ToString())
        };
        y += 2;

        // Priority
        var priorityLabel = new Label { X = 1, Y = y, Text = "Priority:" };
        _priorityField = new TextField
        {
            X = 12,
            Y = y,
            Width = 5,
            Text = issue.Priority?.ToString() ?? ""
        };
        var priorityHint = new Label { X = 18, Y = y, Text = "(1-5, optional)" };

        // Group (on same row)
        var groupLabel = new Label { X = 35, Y = y, Text = "Group:" };
        _groupField = new TextField
        {
            X = 43,
            Y = y,
            Width = Dim.Fill(2),
            Text = issue.Group ?? ""
        };
        y += 2;

        // Assigned To
        var assignedLabel = new Label { X = 1, Y = y, Text = "Assigned:" };
        _assignedField = new TextField
        {
            X = 12,
            Y = y,
            Width = 25,
            Text = issue.AssignedTo ?? ""
        };

        // Tags (on same row)
        var tagsLabel = new Label { X = 40, Y = y, Text = "Tags:" };
        _tagsField = new TextField
        {
            X = 47,
            Y = y,
            Width = Dim.Fill(2),
            Text = issue.Tags.Count > 0 ? string.Join(", ", issue.Tags) : ""
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
            WordWrap = true,
            Text = issue.Description ?? ""
        };
        descFrame.Add(_descriptionView);

        // Buttons
        var saveButton = new Button
        {
            Text = "Save",
            IsDefault = true
        };
        saveButton.Accepting += OnSave;

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
        AddButton(saveButton);

        _titleField.SetFocus();
    }

    private void OnSave(object? sender, CommandEventArgs e)
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
            // Update the issue
            _issueService.UpdateAsync(
                id: _issue.Id,
                title: title != _issue.Title ? title : null,
                type: type != _issue.Type ? type : null,
                status: status != _issue.Status ? status : null,
                description: description != _issue.Description ? description : null,
                priority: priority != _issue.Priority ? priority : null,
                group: group != _issue.Group ? group : null,
                assignedTo: assignedTo != _issue.AssignedTo ? assignedTo : null,
                tags: tags
            ).GetAwaiter().GetResult();

            WasSaved = true;
            RequestStop();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to save issue: {ex.Message}", "OK");
        }
    }
}
