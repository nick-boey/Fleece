using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// Command to find issues that can be worked on next based on dependencies and execution mode.
/// </summary>
public sealed class NextCommand(IFleeceService fleeceService, IAnsiConsole console) : AsyncCommand<NextSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NextSettings settings)
    {
        var (hasMultiple, message) = await fleeceService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            console.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        string? resolvedParentId = null;
        if (!string.IsNullOrWhiteSpace(settings.Parent))
        {
            var matches = await fleeceService.ResolveByPartialIdAsync(settings.Parent);

            if (matches.Count == 0)
            {
                console.MarkupLine($"[red]Error:[/] Parent issue '{settings.Parent}' not found");
                return 1;
            }

            if (matches.Count > 1)
            {
                console.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.Parent}':");
                TableFormatter.RenderIssues(console, matches);
                return 1;
            }

            resolvedParentId = matches[0].Id;
        }

        if (settings.OneLine && (settings.Json || settings.JsonVerbose))
        {
            console.MarkupLine("[red]Error:[/] --one-line cannot be used with --json or --json-verbose");
            return 1;
        }

        GraphSortConfig? sortConfig = null;
        if (!string.IsNullOrWhiteSpace(settings.Sort))
        {
            try
            {
                sortConfig = GraphSortConfig.Parse(settings.Sort);
            }
            catch (ArgumentException ex)
            {
                console.MarkupLine($"[red]Error:[/] {ex.Message}");
                return 1;
            }
        }

        var issues = await fleeceService.GetNextIssuesAsync(resolvedParentId, sortConfig: sortConfig);

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
                console.MarkupLine("[dim]No actionable issues found.[/]");
            }
            else
            {
                console.MarkupLine($"[bold]Actionable issues ({issues.Count}):[/]");
                TableFormatter.RenderIssues(console, issues);
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
            Console.WriteLine($"{issue.Id} {issue.Status.ToString().ToLowerInvariant()} {issue.Type.ToString().ToLowerInvariant()} {issue.Title}");
        }
    }
}
