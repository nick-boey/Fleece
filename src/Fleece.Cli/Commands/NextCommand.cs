using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// Command to find issues that can be worked on next based on dependencies and execution mode.
/// </summary>
public sealed class NextCommand(INextService nextService, IIssueService issueService, IStorageService storageService) : AsyncCommand<NextSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NextSettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        // Resolve parent ID if provided
        string? resolvedParentId = null;
        if (!string.IsNullOrWhiteSpace(settings.Parent))
        {
            var matches = await issueService.ResolveByPartialIdAsync(settings.Parent);

            if (matches.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Parent issue '{settings.Parent}' not found");
                return 1;
            }

            if (matches.Count > 1)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.Parent}':");
                TableFormatter.RenderIssues(matches);
                return 1;
            }

            resolvedParentId = matches[0].Id;
        }

        // Validate mutually exclusive options
        if (settings.OneLine && (settings.Json || settings.JsonVerbose))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --one-line cannot be used with --json or --json-verbose");
            return 1;
        }

        var issues = await nextService.GetNextIssuesAsync(resolvedParentId);

        if (settings.Json || settings.JsonVerbose)
        {
            JsonFormatter.RenderIssues(issues, verbose: settings.JsonVerbose);
        }
        else if (settings.OneLine)
        {
            RenderOneLine(issues);
        }
        else
        {
            if (issues.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]No actionable issues found.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[bold]Actionable issues ({issues.Count}):[/]");
                TableFormatter.RenderIssues(issues);
            }
        }

        return 0;
    }

    private static void RenderOneLine(IReadOnlyList<Core.Models.Issue> issues)
    {
        if (issues.Count == 0)
        {
            return;
        }

        foreach (var issue in issues)
        {
            var groupDisplay = issue.Group ?? "-";
            Console.WriteLine($"{issue.Id} {issue.Status.ToString().ToLowerInvariant()} {groupDisplay} {issue.Type.ToString().ToLowerInvariant()} {issue.Title}");
        }
    }
}
