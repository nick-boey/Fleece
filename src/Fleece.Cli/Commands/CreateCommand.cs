using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class CreateCommand(IFleeceService fleeceService, ISettingsService settingsService, IGitConfigService gitConfigService, IGitService gitService, IAnsiConsole console) : AsyncCommand<CreateSettings>
{
    private IFleeceService _fleece = fleeceService;

    public override async Task<int> ExecuteAsync(CommandContext context, CreateSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.IssuesFile))
        {
            _fleece = FleeceService.ForFile(settings.IssuesFile, settingsService, gitConfigService);
        }

        var (hasMultiple, message) = await _fleece.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            console.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(settings.Title))
        {
            console.MarkupLine("[red]Error:[/] --title is required. See 'fleece create --help'.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(settings.Type))
        {
            console.MarkupLine("[red]Error:[/] --type is required. See 'fleece create --help'.");
            return 1;
        }

        return await CreateFromSettingsAsync(settings);
    }

    private async Task<int> CreateFromSettingsAsync(CreateSettings settings)
    {
        if (!Enum.TryParse<IssueType>(settings.Type, ignoreCase: true, out var issueType))
        {
            console.MarkupLine($"[red]Error:[/] Invalid type '{settings.Type}'. Use: task, bug, chore, feature, idea, verify");
            return 1;
        }

        IssueStatus status;
        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            if (!Enum.TryParse<IssueStatus>(settings.Status, ignoreCase: true, out status))
            {
                console.MarkupLine($"[red]Error:[/] Invalid status '{settings.Status}'. Use: draft, open, progress, review, complete, archived, closed");
                return 1;
            }
        }
        else
        {
            status = string.IsNullOrWhiteSpace(settings.Description) ? IssueStatus.Draft : IssueStatus.Open;
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
                console.MarkupLine($"[red]Error:[/] Invalid execution mode '{settings.ExecutionMode}'. Use: series, parallel");
                return 1;
            }
            executionMode = parsedMode;
        }

        try
        {
            var issue = await _fleece.CreateAsync(
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
                console.MarkupLine($"[green]Created issue[/] [bold]{issue.Id}[/]");
                TableFormatter.RenderIssue(console, issue);
            }

            HandleGitCommitPush(settings, issue.Title);

            return 0;
        }
        catch (ArgumentException ex) when (ex.ParamName == "workingBranchId")
        {
            console.MarkupLine($"[red]Error:[/] {ex.Message}");
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
            console.MarkupLine($"[yellow]Warning:[/] Issue created but git operation failed: {gitResult.ErrorMessage}");
        }
        else
        {
            console.MarkupLine("[dim]Changes committed to git[/]");
            if (settings.Push)
            {
                console.MarkupLine("[dim]Pushed to remote[/]");
            }
        }
    }
}
