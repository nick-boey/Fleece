using Fleece.Cli.Output;
using Fleece.Cli.Services;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class EditCommand(IIssueService issueService, IStorageService storageService) : AsyncCommand<EditSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, EditSettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        // If no options are provided (only the ID), use editor-based editing
        if (HasNoOptions(settings))
        {
            return await EditWithEditorAsync(settings);
        }

        IssueStatus? status = null;
        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            if (!Enum.TryParse<IssueStatus>(settings.Status, ignoreCase: true, out var parsedStatus))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{settings.Status}'. Use: idea, spec, next, progress, review, complete, archived, closed");
                return 1;
            }
            status = parsedStatus;
        }

        IssueType? type = null;
        if (!string.IsNullOrWhiteSpace(settings.Type))
        {
            if (!Enum.TryParse<IssueType>(settings.Type, ignoreCase: true, out var parsedType))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid type '{settings.Type}'. Use: task, bug, chore, feature");
                return 1;
            }
            type = parsedType;
        }

        IReadOnlyList<string>? linkedIssues = null;
        if (!string.IsNullOrWhiteSpace(settings.LinkedIssues))
        {
            linkedIssues = settings.LinkedIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        IReadOnlyList<string>? parentIssues = null;
        if (!string.IsNullOrWhiteSpace(settings.ParentIssues))
        {
            parentIssues = settings.ParentIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        IReadOnlyList<string>? previousIssues = null;
        if (!string.IsNullOrWhiteSpace(settings.PreviousIssues))
        {
            previousIssues = settings.PreviousIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        IReadOnlyList<string>? tags = null;
        if (!string.IsNullOrWhiteSpace(settings.Tags))
        {
            tags = settings.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        try
        {
            var issue = await issueService.UpdateAsync(
                id: settings.Id,
                title: settings.Title,
                description: settings.Description,
                status: status,
                type: type,
                priority: settings.Priority,
                linkedPr: settings.LinkedPr,
                linkedIssues: linkedIssues,
                parentIssues: parentIssues,
                previousIssues: previousIssues,
                group: settings.Group,
                assignedTo: settings.AssignedTo,
                tags: tags,
                workingBranchId: settings.WorkingBranchId);

            if (settings.Json || settings.JsonVerbose)
            {
                JsonFormatter.RenderIssue(issue, verbose: settings.JsonVerbose);
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Updated issue[/] [bold]{issue.Id}[/]");
                TableFormatter.RenderIssue(issue);
            }

            return 0;
        }
        catch (KeyNotFoundException)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
            return 1;
        }
        catch (ArgumentException ex) when (ex.ParamName == "workingBranchId")
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static bool HasNoOptions(EditSettings settings) =>
        string.IsNullOrWhiteSpace(settings.Title) &&
        string.IsNullOrWhiteSpace(settings.Description) &&
        string.IsNullOrWhiteSpace(settings.Status) &&
        string.IsNullOrWhiteSpace(settings.Type) &&
        !settings.Priority.HasValue &&
        !settings.LinkedPr.HasValue &&
        string.IsNullOrWhiteSpace(settings.LinkedIssues) &&
        string.IsNullOrWhiteSpace(settings.ParentIssues) &&
        string.IsNullOrWhiteSpace(settings.PreviousIssues) &&
        string.IsNullOrWhiteSpace(settings.Group) &&
        string.IsNullOrWhiteSpace(settings.AssignedTo) &&
        string.IsNullOrWhiteSpace(settings.Tags) &&
        string.IsNullOrWhiteSpace(settings.WorkingBranchId) &&
        !settings.Json &&
        !settings.JsonVerbose;

    private async Task<int> EditWithEditorAsync(EditSettings settings)
    {
        // First, get the existing issue
        Issue existingIssue;
        try
        {
            var issues = await issueService.GetAllAsync();
            var found = issues.FirstOrDefault(i => i.Id.Equals(settings.Id, StringComparison.OrdinalIgnoreCase));
            if (found is null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
                return 1;
            }
            existingIssue = found;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to load issue: {ex.Message}");
            return 1;
        }

        var editorService = new EditorService();
        var templatePath = editorService.CreateEditTemplateFile(existingIssue);

        AnsiConsole.MarkupLine($"[dim]Opening editor... Edit the fields and save to update the issue.[/]");
        AnsiConsole.MarkupLine($"[dim]Template: {templatePath}[/]");

        try
        {
            editorService.OpenEditor(templatePath);

            var template = editorService.ParseTemplate(templatePath);

            if (template is null)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] Template was empty or could not be parsed. No changes made.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(template.Title))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Title is required");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(template.Type))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Type is required");
                return 1;
            }

            if (!Enum.TryParse<IssueType>(template.Type, ignoreCase: true, out var issueType))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid type '{template.Type}'. Use: task, bug, chore, feature");
                return 1;
            }

            IssueStatus? status = null;
            if (!string.IsNullOrWhiteSpace(template.Status))
            {
                if (!Enum.TryParse<IssueStatus>(template.Status, ignoreCase: true, out var parsedStatus))
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{template.Status}'. Use: idea, spec, next, progress, review, complete, archived, closed");
                    return 1;
                }
                status = parsedStatus;
            }

            IReadOnlyList<string>? linkedIssues = null;
            if (!string.IsNullOrWhiteSpace(template.LinkedIssues))
            {
                linkedIssues = template.LinkedIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            else
            {
                linkedIssues = [];
            }

            IReadOnlyList<string>? parentIssues = null;
            if (!string.IsNullOrWhiteSpace(template.ParentIssues))
            {
                parentIssues = template.ParentIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            else
            {
                parentIssues = [];
            }

            IReadOnlyList<string>? previousIssues = null;
            if (!string.IsNullOrWhiteSpace(template.PreviousIssues))
            {
                previousIssues = template.PreviousIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            else
            {
                previousIssues = [];
            }

            IReadOnlyList<string>? tags = null;
            if (!string.IsNullOrWhiteSpace(template.Tags))
            {
                tags = template.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            else
            {
                tags = [];
            }

            var issue = await issueService.UpdateAsync(
                id: settings.Id,
                title: template.Title,
                description: template.Description,
                status: status,
                type: issueType,
                priority: template.Priority,
                linkedPr: template.LinkedPr,
                linkedIssues: linkedIssues,
                parentIssues: parentIssues,
                previousIssues: previousIssues,
                group: template.Group,
                assignedTo: template.AssignedTo,
                tags: tags,
                workingBranchId: template.WorkingBranchId);

            AnsiConsole.MarkupLine($"[green]Updated issue[/] [bold]{issue.Id}[/]");
            TableFormatter.RenderIssue(issue);

            return 0;
        }
        catch (KeyNotFoundException)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
            return 1;
        }
        catch (ArgumentException ex) when (ex.ParamName == "workingBranchId")
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        finally
        {
            editorService.CleanupTemplateFile(templatePath);
        }
    }
}
