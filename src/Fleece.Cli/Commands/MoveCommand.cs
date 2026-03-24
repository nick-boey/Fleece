using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class MoveCommand(
    IDependencyService dependencyService,
    IIssueService issueService,
    IStorageServiceProvider storageServiceProvider)
    : AsyncCommand<MoveSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, MoveSettings settings)
    {
        var storageService = storageServiceProvider.GetStorageService(settings.IssuesFile);
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        try
        {
            // Resolve the parent ID
            var parentId = settings.ParentId;
            if (string.IsNullOrWhiteSpace(parentId))
            {
                parentId = await InferSingleParentAsync(settings.IssueId);
            }

            MoveResult result;
            if (settings.Up)
            {
                result = await dependencyService.MoveUpAsync(parentId, settings.IssueId);
            }
            else
            {
                result = await dependencyService.MoveDownAsync(parentId, settings.IssueId);
            }

            if (result.Outcome == MoveOutcome.Invalid)
            {
                if (settings.Json)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        outcome = result.Outcome.ToString(),
                        reason = result.Reason.ToString(),
                        message = result.Message
                    });
                    Console.WriteLine(json);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]{result.Message}[/]");
                }
                return 1;
            }

            if (settings.Json)
            {
                JsonFormatter.RenderIssue(result.UpdatedIssue!);
            }
            else
            {
                var direction = result.Outcome == MoveOutcome.MovedUp ? "up" : "down";
                AnsiConsole.MarkupLine(
                    $"[green]Moved[/] [bold]{result.UpdatedIssue!.Id}[/] {direction} within [bold]{parentId}[/]");
                TableFormatter.RenderIssue(result.UpdatedIssue);
            }

            return 0;
        }
        catch (KeyNotFoundException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<string> InferSingleParentAsync(string issueId)
    {
        var matches = await issueService.ResolveByPartialIdAsync(issueId);

        if (matches.Count == 0)
        {
            throw new KeyNotFoundException($"No issue found matching '{issueId}'");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple issues match '{issueId}': {string.Join(", ", matches.Select(m => m.Id))}");
        }

        var issue = matches[0];

        if (issue.ParentIssues.Count == 0)
        {
            throw new InvalidOperationException(
                $"Issue '{issue.Id}' has no parents. Use --parent to specify a parent.");
        }

        if (issue.ParentIssues.Count > 1)
        {
            throw new InvalidOperationException(
                $"Issue '{issue.Id}' has multiple parents. Use --parent to specify which parent to reorder within.");
        }

        return issue.ParentIssues[0].ParentIssue;
    }
}
