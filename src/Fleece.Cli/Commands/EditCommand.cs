using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class EditCommand(IFleeceService fleeceService, ISettingsService settingsService, IGitConfigService gitConfigService, IAnsiConsole console) : AsyncCommand<EditSettings>
{
    private IFleeceService _fleece = fleeceService;

    public override async Task<int> ExecuteAsync(CommandContext context, EditSettings settings)
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

        var matches = await _fleece.ResolveByPartialIdAsync(settings.Id);

        if (matches.Count == 0)
        {
            console.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
            return 1;
        }

        if (matches.Count > 1)
        {
            console.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.Id}':");
            TableFormatter.RenderIssues(console, matches);
            return 1;
        }

        var resolvedId = matches[0].Id;

        if (!HasAnyFieldFlag(settings))
        {
            console.MarkupLine(
                "[red]Error:[/] edit requires at least one field flag " +
                "(--title, --description, --status, --type, --priority, --linked-issues, " +
                "--linked-pr, --assign, --tags, --working-branch, --execution-mode). " +
                "See 'fleece edit --help'.");
            return 1;
        }

        IssueStatus? status = null;
        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            if (!Enum.TryParse<IssueStatus>(settings.Status, ignoreCase: true, out var parsedStatus))
            {
                console.MarkupLine($"[red]Error:[/] Invalid status '{settings.Status}'. Use: draft, open, progress, review, complete, archived, closed");
                return 1;
            }
            status = parsedStatus;
        }

        IssueType? type = null;
        if (!string.IsNullOrWhiteSpace(settings.Type))
        {
            if (!Enum.TryParse<IssueType>(settings.Type, ignoreCase: true, out var parsedType))
            {
                console.MarkupLine($"[red]Error:[/] Invalid type '{settings.Type}'. Use: task, bug, chore, feature, idea, verify");
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
                console.MarkupLine($"[red]Error:[/] Invalid execution mode '{settings.ExecutionMode}'. Use: series, parallel");
                return 1;
            }
            executionMode = parsedMode;
        }

        try
        {
            var issue = await _fleece.UpdateAsync(
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
                console.MarkupLine($"[green]Updated issue[/] [bold]{issue.Id}[/]");
                TableFormatter.RenderIssue(console, issue);
            }

            return 0;
        }
        catch (KeyNotFoundException)
        {
            console.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
            return 1;
        }
        catch (ArgumentException ex) when (ex.ParamName == "workingBranchId")
        {
            console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static bool HasAnyFieldFlag(EditSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.Title) ||
        !string.IsNullOrWhiteSpace(settings.Description) ||
        !string.IsNullOrWhiteSpace(settings.Status) ||
        !string.IsNullOrWhiteSpace(settings.Type) ||
        settings.Priority.HasValue ||
        settings.LinkedPr.HasValue ||
        !string.IsNullOrWhiteSpace(settings.LinkedIssues) ||
        !string.IsNullOrWhiteSpace(settings.AssignedTo) ||
        !string.IsNullOrWhiteSpace(settings.Tags) ||
        !string.IsNullOrWhiteSpace(settings.WorkingBranchId) ||
        !string.IsNullOrWhiteSpace(settings.ExecutionMode);
}
