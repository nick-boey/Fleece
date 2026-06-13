using System.Text;
using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// Escalates one or more Fleece issues into a single GitHub issue and marks them <c>Promoted</c>
/// with a <c>promoted=&lt;github-#&gt;</c> tag. Idempotent: an issue already carrying a
/// <c>promoted=</c> tag is skipped with a warning.
/// </summary>
public sealed class PromoteCommand(
    IFleeceService fleeceService,
    IGitHubService gitHubService,
    IAnsiConsole console) : AsyncCommand<PromoteSettings>
{
    private const string PromotedTagKey = "promoted";

    public override async Task<int> ExecuteAsync(CommandContext context, PromoteSettings settings)
    {
        if (settings.Ids.Length == 0)
        {
            console.MarkupLine("[red]Error:[/] At least one issue ID is required");
            return 1;
        }

        var auth = await gitHubService.ResolveAuthAsync();
        if (!auth.Authenticated)
        {
            console.MarkupLine("[red]Error:[/] Not authenticated with GitHub. Run 'fleece auth' for guidance.");
            return 1;
        }

        // Resolve every supplied ID up front so a typo fails the whole command before any GitHub call.
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
            if (KeyedTag.HasKey(issue.Tags, PromotedTagKey))
            {
                var existing = KeyedTag.GetValues(issue.Tags, PromotedTagKey).FirstOrDefault() ?? "?";
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

        var root = toPromote[0];
        var body = BuildIssueBody(toPromote);
        var issueRef = await gitHubService.CreateIssueAsync(root.Title, body);

        var promotedIds = new List<string>();
        foreach (var issue in toPromote)
        {
            var tags = KeyedTag.AddValue(issue.Tags, PromotedTagKey, issueRef.Number.ToString());
            await fleeceService.UpdateAsync(
                id: issue.Id,
                status: IssueStatus.Promoted,
                tags: tags);
            promotedIds.Add(issue.Id);
        }

        if (settings.Json)
        {
            console.WriteLine(JsonSerializer.Serialize(new
            {
                githubIssue = new { number = issueRef.Number, url = issueRef.Url },
                promoted = promotedIds,
            }));
            return 0;
        }

        console.MarkupLine($"[green]Created GitHub issue[/] [bold]#{issueRef.Number}[/] [dim]{issueRef.Url.EscapeMarkup()}[/]");
        console.MarkupLine($"[green]Promoted {promotedIds.Count} issue(s):[/] {string.Join(", ", promotedIds).EscapeMarkup()}");
        return 0;
    }

    /// <summary>
    /// Composes the GitHub issue body from the promoted bundle. Each issue is rendered as a
    /// task-list item (id + title + type/priority) followed by its full description, indented so it
    /// nests under the checklist item. Issues without a description get an explicit placeholder so
    /// the GitHub issue is never an empty stub.
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
