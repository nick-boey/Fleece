using Fleece.Cli.Output;
using Fleece.Cli.Services;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class EditCommand(IIssueServiceFactory issueServiceFactory, IStorageServiceProvider storageServiceProvider) : AsyncCommand<EditSettings>
{
    private IIssueService? _issueService;

    public override async Task<int> ExecuteAsync(CommandContext context, EditSettings settings)
    {
        var storageService = storageServiceProvider.GetStorageService(settings.IssuesFile);
        _issueService = issueServiceFactory.GetIssueService(settings.IssuesFile);
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        // Resolve partial ID first
        var matches = await _issueService!.ResolveByPartialIdAsync(settings.Id);

        if (matches.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
            return 1;
        }

        if (matches.Count > 1)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.Id}':");
            TableFormatter.RenderIssues(matches);
            return 1;
        }

        var resolvedId = matches[0].Id;

        // If no options are provided (only the ID), use editor-based editing
        if (HasNoOptions(settings))
        {
            return await EditWithEditorAsync(settings, resolvedId);
        }

        IssueStatus? status = null;
        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            if (!Enum.TryParse<IssueStatus>(settings.Status, ignoreCase: true, out var parsedStatus))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{settings.Status}'. Use: open, progress, review, complete, archived, closed");
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

        IReadOnlyList<string>? tags = null;
        if (!string.IsNullOrWhiteSpace(settings.Tags))
        {
            tags = settings.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        ExecutionMode? executionMode = null;
        if (!string.IsNullOrWhiteSpace(settings.ExecutionMode))
        {
            if (!Enum.TryParse<ExecutionMode>(settings.ExecutionMode, ignoreCase: true, out var parsedMode))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid execution mode '{settings.ExecutionMode}'. Use: series, parallel");
                return 1;
            }
            executionMode = parsedMode;
        }

        try
        {
            var issue = await _issueService!.UpdateAsync(
                id: resolvedId,
                title: settings.Title,
                description: settings.Description,
                status: status,
                type: type,
                priority: settings.Priority,
                linkedPr: settings.LinkedPr,
                linkedIssues: linkedIssues,
                assignedTo: settings.AssignedTo,
                tags: tags,
                workingBranchId: settings.WorkingBranchId,
                executionMode: executionMode);

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
        string.IsNullOrWhiteSpace(settings.AssignedTo) &&
        string.IsNullOrWhiteSpace(settings.Tags) &&
        string.IsNullOrWhiteSpace(settings.WorkingBranchId) &&
        string.IsNullOrWhiteSpace(settings.ExecutionMode) &&
        !settings.Json &&
        !settings.JsonVerbose;

    private async Task<int> EditWithEditorAsync(EditSettings settings, string resolvedId)
    {
        // Get the existing issue using the already-resolved ID
        var existingIssue = await _issueService!.GetByIdAsync(resolvedId);
        if (existingIssue is null)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{resolvedId}' not found");
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
                    AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{template.Status}'. Use: open, progress, review, complete, archived, closed");
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

            IReadOnlyList<string>? tags = null;
            if (!string.IsNullOrWhiteSpace(template.Tags))
            {
                tags = template.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            else
            {
                tags = [];
            }

            var issue = await _issueService!.UpdateAsync(
                id: resolvedId,
                title: template.Title,
                description: template.Description,
                status: status,
                type: issueType,
                priority: template.Priority,
                linkedPr: template.LinkedPr,
                linkedIssues: linkedIssues,
                assignedTo: template.AssignedTo,
                tags: tags,
                workingBranchId: template.WorkingBranchId);

            AnsiConsole.MarkupLine($"[green]Updated issue[/] [bold]{issue.Id}[/]");
            TableFormatter.RenderIssue(issue);

            return 0;
        }
        catch (KeyNotFoundException)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{resolvedId}' not found");
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
