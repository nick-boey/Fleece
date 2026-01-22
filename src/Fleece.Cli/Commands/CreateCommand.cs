using Fleece.Cli.Output;
using Fleece.Cli.Services;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class CreateCommand(IIssueService issueService, IStorageService storageService) : AsyncCommand<CreateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CreateSettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        // If no title and no type provided, use editor-based creation
        if (string.IsNullOrWhiteSpace(settings.Title) && string.IsNullOrWhiteSpace(settings.Type))
        {
            return await CreateWithEditorAsync(settings);
        }

        // Otherwise, require both title and type
        if (string.IsNullOrWhiteSpace(settings.Title))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --title is required");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(settings.Type))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --type is required");
            return 1;
        }

        return await CreateFromSettingsAsync(settings);
    }

    private async Task<int> CreateWithEditorAsync(CreateSettings settings)
    {
        var editorService = new EditorService();
        var templatePath = editorService.CreateTemplateFile();

        AnsiConsole.MarkupLine($"[dim]Opening editor... Edit the template and save to create an issue.[/]");
        AnsiConsole.MarkupLine($"[dim]Template: {templatePath}[/]");

        try
        {
            editorService.OpenEditor(templatePath);

            var template = editorService.ParseTemplate(templatePath);

            if (template is null)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] Template was empty or could not be parsed. No issue created.");
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
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid type '{template.Type}'. Use: task, bug, chore, idea, feature");
                return 1;
            }

            var status = IssueStatus.Open;
            if (!string.IsNullOrWhiteSpace(template.Status))
            {
                if (!Enum.TryParse<IssueStatus>(template.Status, ignoreCase: true, out status))
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{template.Status}'. Use: open, complete, closed, archived");
                    return 1;
                }
            }

            IReadOnlyList<string>? linkedIssues = null;
            if (!string.IsNullOrWhiteSpace(template.LinkedIssues))
            {
                linkedIssues = template.LinkedIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            IReadOnlyList<string>? parentIssues = null;
            if (!string.IsNullOrWhiteSpace(template.ParentIssues))
            {
                parentIssues = template.ParentIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            IReadOnlyList<string>? tags = null;
            if (!string.IsNullOrWhiteSpace(template.Tags))
            {
                tags = template.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            await storageService.EnsureDirectoryExistsAsync();

            var issue = await issueService.CreateAsync(
                title: template.Title,
                type: issueType,
                description: template.Description,
                status: status,
                priority: template.Priority,
                linkedPr: template.LinkedPr,
                linkedIssues: linkedIssues,
                parentIssues: parentIssues,
                group: template.Group,
                assignedTo: template.AssignedTo,
                tags: tags,
                workingBranchId: template.WorkingBranchId);

            if (settings.Json || settings.JsonVerbose)
            {
                JsonFormatter.RenderIssue(issue, verbose: settings.JsonVerbose);
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Created issue[/] [bold]{issue.Id}[/]");
                TableFormatter.RenderIssue(issue);
            }

            return 0;
        }
        finally
        {
            editorService.CleanupTemplateFile(templatePath);
        }
    }

    private async Task<int> CreateFromSettingsAsync(CreateSettings settings)
    {
        if (!Enum.TryParse<IssueType>(settings.Type, ignoreCase: true, out var issueType))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid type '{settings.Type}'. Use: task, bug, chore, idea, feature");
            return 1;
        }

        var status = IssueStatus.Open;
        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            if (!Enum.TryParse<IssueStatus>(settings.Status, ignoreCase: true, out status))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{settings.Status}'. Use: open, complete, closed, archived");
                return 1;
            }
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

        IReadOnlyList<string>? tags = null;
        if (!string.IsNullOrWhiteSpace(settings.Tags))
        {
            tags = settings.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        await storageService.EnsureDirectoryExistsAsync();

        try
        {
            var issue = await issueService.CreateAsync(
                title: settings.Title,
                type: issueType,
                description: settings.Description,
                status: status,
                priority: settings.Priority,
                linkedPr: settings.LinkedPr,
                linkedIssues: linkedIssues,
                parentIssues: parentIssues,
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
                AnsiConsole.MarkupLine($"[green]Created issue[/] [bold]{issue.Id}[/]");
                TableFormatter.RenderIssue(issue);
            }

            return 0;
        }
        catch (ArgumentException ex) when (ex.ParamName == "workingBranchId")
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
