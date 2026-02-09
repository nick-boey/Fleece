using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class ListCommand(IIssueService issueService, IStorageService storageService) : AsyncCommand<ListSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        // Load issues with diagnostics
        var loadResult = await storageService.LoadIssuesWithDiagnosticsAsync();
        var hasWarnings = DiagnosticFormatter.RenderDiagnostics(loadResult.Diagnostics);

        // Fail early in strict mode if there are warnings
        if (settings.Strict && hasWarnings)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Schema warnings detected in strict mode.");
            return 1;
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

        // Validate mutually exclusive options
        if (settings.OneLine && (settings.Json || settings.JsonVerbose))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --one-line cannot be used with --json or --json-verbose");
            return 1;
        }

        // Apply filtering via the issue service
        var issues = await issueService.FilterAsync(
            status,
            type,
            settings.Priority,
            settings.AssignedTo,
            settings.Tags,
            settings.LinkedPr,
            settings.All);

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
            TableFormatter.RenderIssues(issues);
        }

        return 0;
    }

    private static void RenderOneLine(IReadOnlyList<Issue> issues)
    {
        if (issues.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No issues found[/]");
            return;
        }

        foreach (var issue in issues)
        {
            Console.WriteLine($"{issue.Id} {issue.Status.ToString().ToLowerInvariant()} {issue.Type.ToString().ToLowerInvariant()} {issue.Title}");
        }
    }
}
