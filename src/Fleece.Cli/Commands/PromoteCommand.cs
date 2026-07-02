using System.Text;
using Fleece.Cli.Settings;
using Fleece.Cli.Workflows;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// Escalates one or more Fleece issues into the repository's durable tracker and marks them
/// <c>Promoted</c> with a <c>promoted=&lt;ref&gt;</c> tag. The command resolves and filters the
/// bundle (already-promoted issues are skipped with a warning), formats the shared issue body, then
/// hands the tracker-specific escalation to the configured <see cref="ITrackerWorkflow"/>.
/// </summary>
public sealed class PromoteCommand(
    IFleeceService fleeceService,
    ITrackerWorkflow trackerWorkflow,
    IAnsiConsole console) : AsyncCommand<PromoteSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, PromoteSettings settings)
    {
        if (settings.Ids.Length == 0)
        {
            console.MarkupLine("[red]Error:[/] At least one issue ID is required");
            return 1;
        }

        // Tracker-specific preflight runs before ID resolution: GitHub rejects --ref and fails fast
        // when unauthenticated (so a typo'd ID can't mask an auth failure); Linear is a no-op.
        var preflight = await trackerWorkflow.PreparePromoteAsync(new PromotePreflight(settings.Ref, settings.Json));
        if (preflight is int exitCode)
        {
            return exitCode;
        }

        // Resolve every supplied ID up front so a typo fails the whole command before any tracker work.
        var resolved = new List<Issue>();
        foreach (var id in settings.Ids)
        {
            var matches = await fleeceService.ResolveByPartialIdAsync(id);
            if (matches.Count == 0)
            {
                console.MarkupLine($"[red]Error:[/] Issue '{id.EscapeMarkup()}' not found");
                return 1;
            }

            if (matches.Count > 1)
            {
                var matchingIds = string.Join(", ", matches.Select(m => m.Id));
                console.MarkupLine($"[red]Error:[/] Multiple issues match '{id.EscapeMarkup()}': {matchingIds.EscapeMarkup()}");
                return 1;
            }

            resolved.Add(matches[0]);
        }

        var toPromote = new List<Issue>();
        foreach (var issue in resolved)
        {
            if (KeyedTag.HasKey(issue.Tags, TrackerWorkflow.PromotedTagKey))
            {
                var existing = KeyedTag.GetValues(issue.Tags, TrackerWorkflow.PromotedTagKey).FirstOrDefault() ?? "?";
                console.MarkupLine($"[yellow]Warning:[/] Issue '{issue.Id}' is already promoted (promoted={existing.EscapeMarkup()}); skipping");
            }
            else
            {
                toPromote.Add(issue);
            }
        }

        if (toPromote.Count == 0)
        {
            console.MarkupLine("[yellow]Nothing to promote — all supplied issues are already promoted.[/]");
            return 0;
        }

        var body = BuildIssueBody(toPromote);
        return await trackerWorkflow.PromoteAsync(new PromoteContext(toPromote, body, settings.Ref, settings.Json));
    }

    /// <summary>
    /// Composes the durable issue body from the promoted bundle. Each issue is rendered as a
    /// task-list item (id + title + type/priority) followed by its full description, indented so it
    /// nests under the checklist item. Issues without a description get an explicit placeholder so
    /// the created issue is never an empty stub.
    /// </summary>
    private static string BuildIssueBody(IReadOnlyList<Issue> issues)
    {
        var sb = new StringBuilder();
        sb.AppendLine("_Promoted from Fleece branch-local working memory._");
        sb.AppendLine();

        foreach (var issue in issues)
        {
            var type = issue.Type.ToString().ToLowerInvariant();
            var meta = issue.Priority is int priority ? $"{type} · priority {priority}" : type;

            sb.Append("- [ ] **").Append(issue.Id).Append("** ")
                .Append(issue.Title).Append(" _(").Append(meta).AppendLine(")_");
            sb.AppendLine();

            var description = string.IsNullOrWhiteSpace(issue.Description)
                ? "_No description provided._"
                : issue.Description.Trim();

            foreach (var line in description.Replace("\r\n", "\n").Split('\n'))
            {
                // Two-space indent keeps the description nested under its checklist item in GitHub
                // markdown; blank lines stay blank so paragraph breaks survive.
                sb.AppendLine(line.Length == 0 ? string.Empty : "  " + line.TrimEnd());
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
