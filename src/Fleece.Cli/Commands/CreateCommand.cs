using Fleece.Cli.Output;
using Fleece.Cli.Services;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class CreateCommand(IIssueServiceFactory issueServiceFactory, IStorageServiceProvider storageServiceProvider, IGitService gitService) : AsyncCommand<CreateSettings>
{
    private IStorageService? _storageService;
    private IIssueService? _issueService;

    public override async Task<int> ExecuteAsync(CommandContext context, CreateSettings settings)
    {
        _storageService = storageServiceProvider.GetStorageService(settings.IssuesFile);
        _issueService = issueServiceFactory.GetIssueService(settings.IssuesFile);
        var (hasMultiple, message) = await _storageService.HasMultipleUnmergedFilesAsync();
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
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid type '{template.Type}'. Use: task, bug, chore, feature");
                return 1;
            }

            var status = IssueStatus.Open;
            if (!string.IsNullOrWhiteSpace(template.Status))
            {
                if (!Enum.TryParse<IssueStatus>(template.Status, ignoreCase: true, out status))
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{template.Status}'. Use: open, progress, review, complete, archived, closed");
                    return 1;
                }
            }

            IReadOnlyList<string>? linkedIssues = null;
            if (!string.IsNullOrWhiteSpace(template.LinkedIssues))
            {
                linkedIssues = template.LinkedIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            var parentIssues = ParentIssueRef.ParseFromStrings(template.ParentIssues);

            IReadOnlyList<string>? tags = null;
            if (!string.IsNullOrWhiteSpace(template.Tags))
            {
                tags = template.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            await _storageService!.EnsureDirectoryExistsAsync();

            var issue = await _issueService!.CreateAsync(
                title: template.Title,
                type: issueType,
                description: template.Description,
                status: status,
                priority: template.Priority,
                linkedPr: template.LinkedPr,
                linkedIssues: linkedIssues,
                parentIssues: parentIssues.Count > 0 ? parentIssues : null,
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

            // Handle git commit/push if requested
            HandleGitCommitPush(settings, issue.Title);

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
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid type '{settings.Type}'. Use: task, bug, chore, feature");
            return 1;
        }

        var status = IssueStatus.Open;
        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            if (!Enum.TryParse<IssueStatus>(settings.Status, ignoreCase: true, out status))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{settings.Status}'. Use: open, progress, review, complete, archived, closed");
                return 1;
            }
        }

        IReadOnlyList<string>? linkedIssues = null;
        if (!string.IsNullOrWhiteSpace(settings.LinkedIssues))
        {
            linkedIssues = settings.LinkedIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var parentIssues = ParentIssueRef.ParseFromStrings(settings.ParentIssues);

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

        await _storageService!.EnsureDirectoryExistsAsync();

        try
        {
            var issue = await _issueService!.CreateAsync(
                title: settings.Title,
                type: issueType,
                description: settings.Description,
                status: status,
                priority: settings.Priority,
                linkedPr: settings.LinkedPr,
                linkedIssues: linkedIssues,
                parentIssues: parentIssues.Count > 0 ? parentIssues : null,
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
                AnsiConsole.MarkupLine($"[green]Created issue[/] [bold]{issue.Id}[/]");
                TableFormatter.RenderIssue(issue);
            }

            // Handle git commit/push if requested
            HandleGitCommitPush(settings, issue.Title);

            return 0;
        }
        catch (ArgumentException ex) when (ex.ParamName == "workingBranchId")
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private void HandleGitCommitPush(CreateSettings settings, string issueTitle)
    {
        if (!settings.Commit && !settings.Push)
        {
            return;
        }

        var commitMessage = $"Add issue: {issueTitle}";
        var gitResult = settings.Push
            ? gitService.CommitAndPushFleeceChanges(commitMessage)
            : gitService.CommitFleeceChanges(commitMessage);

        if (!gitResult.Success)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Issue created but git operation failed: {gitResult.ErrorMessage}");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]Changes committed to git[/]");
            if (settings.Push)
            {
                AnsiConsole.MarkupLine("[dim]Pushed to remote[/]");
            }
        }
    }
}
