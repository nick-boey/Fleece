using System.Text.Json;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;

namespace Fleece.Cli.Workflows;

/// <summary>
/// The GitHub durable-tracker workflow. Delegates to the existing <see cref="IGitHubService"/> and
/// preserves the behaviour of the pre-tracker <c>promote</c>/<c>absorb</c>/<c>auth</c> commands: a
/// single created GitHub issue for a promoted bundle, native absorb (comment + assign, no close),
/// and GitHub auth reporting.
/// </summary>
public sealed class GitHubTrackerWorkflow(
    IFleeceService fleeceService,
    IGitHubService gitHubService,
    IGitService gitService,
    IAnsiConsole console) : ITrackerWorkflow
{
    private const string AbsorbedFromTagKey = "absorbed-from";

    public async Task<int> PromoteAsync(PromoteContext context, CancellationToken cancellationToken = default)
    {
        var auth = await gitHubService.ResolveAuthAsync(cancellationToken);
        if (!auth.Authenticated)
        {
            console.MarkupLine("[red]Error:[/] Not authenticated with GitHub. Run 'fleece auth' for guidance.");
            return 1;
        }

        var root = context.Bundle[0];
        var issueRef = await gitHubService.CreateIssueAsync(root.Title, context.Body, cancellationToken);

        await TrackerWorkflow.RecordPromotionAsync(
            fleeceService, context.Bundle, issueRef.Number.ToString(), cancellationToken);
        var promotedIds = context.Bundle.Select(i => i.Id).ToList();

        if (context.Json)
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

    public async Task<int> AbsorbAsync(AbsorbContext context, CancellationToken cancellationToken = default)
    {
        var reference = context.Reference?.Trim() ?? string.Empty;

        // The leading '#' makes the GitHub-number vs Fleece-id namespace split explicit at the point
        // of confusion. A bare number performs NO action.
        if (!reference.StartsWith('#'))
        {
            console.MarkupLine($"[yellow]Warning:[/] '{reference.EscapeMarkup()}' is not a GitHub reference. Re-run as [bold]fleece absorb #{reference.EscapeMarkup()}[/] to absorb GitHub issue {reference.EscapeMarkup()}.");
            return 1;
        }

        if (!int.TryParse(reference[1..], out var number) || number <= 0)
        {
            console.MarkupLine($"[red]Error:[/] '{reference.EscapeMarkup()}' is not a valid GitHub issue reference (expected #<number>).");
            return 1;
        }

        var auth = await gitHubService.ResolveAuthAsync(cancellationToken);
        if (!auth.Authenticated)
        {
            console.MarkupLine("[red]Error:[/] Not authenticated with GitHub. Run 'fleece auth' for guidance.");
            return 1;
        }

        var ghIssue = await gitHubService.GetIssueAsync(number, cancellationToken);

        var branch = gitService.GetCurrentBranch() ?? "(detached HEAD)";

        var issue = await fleeceService.CreateAsync(
            title: ghIssue.Title,
            type: IssueType.Task,
            description: ghIssue.Body,
            tags: [KeyedTag.Create(AbsorbedFromTagKey, number.ToString())],
            cancellationToken: cancellationToken);

        await gitHubService.AddCommentAsync(
            number,
            $"Absorbed into Fleece issue `{issue.Id}` on branch `{branch}`.",
            cancellationToken);

        var login = auth.Login;
        if (!string.IsNullOrWhiteSpace(login))
        {
            await gitHubService.AssignAsync(number, login, cancellationToken);
        }

        if (context.Json)
        {
            console.WriteLine(JsonSerializer.Serialize(new
            {
                fleeceId = issue.Id,
                githubNumber = number,
                branch,
                assignedTo = login,
            }));
            return 0;
        }

        console.MarkupLine($"[green]Absorbed GitHub issue[/] [bold]#{number}[/] [green]into Fleece issue[/] [bold]{issue.Id}[/]");
        console.MarkupLine($"[dim]Commented on #{number} and assigned to {(login ?? "no one").EscapeMarkup()} (issue left open).[/]");
        return 0;
    }

    public async Task<int> AuthAsync(AuthContext context, CancellationToken cancellationToken = default)
    {
        var auth = await gitHubService.ResolveAuthAsync(cancellationToken);

        if (context.Json)
        {
            console.WriteLine(JsonSerializer.Serialize(new
            {
                authenticated = auth.Authenticated,
                login = auth.Login,
                tokenSource = auth.TokenSource,
                repository = auth.Repository,
            }));
            return auth.Authenticated ? 0 : 1;
        }

        if (!auth.Authenticated)
        {
            console.MarkupLine("[red]Not authenticated with GitHub.[/]");
            console.MarkupLine("Provide credentials via one of:");
            console.MarkupLine("  - [bold]gh auth login[/] (GitHub CLI)");
            console.MarkupLine("  - the [bold]GH_TOKEN[/] or [bold]GITHUB_TOKEN[/] environment variable");
            console.MarkupLine("  - a PAT in git config: [bold]git config fleece.githubToken <token>[/]");
            return 1;
        }

        console.MarkupLine($"[green]Authenticated as[/] [bold]{(auth.Login ?? "unknown").EscapeMarkup()}[/]");
        console.MarkupLine($"[dim]Token source:[/] {auth.TokenSource?.EscapeMarkup()}");
        if (!string.IsNullOrWhiteSpace(auth.Repository))
        {
            console.MarkupLine($"[dim]Repository:[/] {auth.Repository.EscapeMarkup()}");
        }

        return 0;
    }
}
