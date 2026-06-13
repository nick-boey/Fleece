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

    private static string BuildIssueBody(IReadOnlyList<Issue> issues)
    {
        var sb = new StringBuilder();
        foreach (var issue in issues)
        {
            sb.Append("- [ ] ").Append(issue.Id).Append(' ').AppendLine(issue.Title);
        }

        return sb.ToString().TrimEnd();
    }
}
