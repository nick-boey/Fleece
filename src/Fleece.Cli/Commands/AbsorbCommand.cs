using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// Converts a GitHub issue (<c>#&lt;github-#&gt;</c>) into a Fleece issue, then comments on and
/// assigns the GitHub issue without closing it. A reference without a leading <c>#</c> performs no
/// action and warns the user to re-run with the <c>#</c> prefix.
/// </summary>
public sealed class AbsorbCommand(
    IFleeceService fleeceService,
    IGitHubService gitHubService,
    IGitService gitService,
    IAnsiConsole console) : AsyncCommand<AbsorbSettings>
{
    private const string AbsorbedFromTagKey = "absorbed-from";

    public override async Task<int> ExecuteAsync(CommandContext context, AbsorbSettings settings)
    {
        var reference = settings.Reference?.Trim() ?? string.Empty;

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

        var auth = await gitHubService.ResolveAuthAsync();
        if (!auth.Authenticated)
        {
            console.MarkupLine("[red]Error:[/] Not authenticated with GitHub. Run 'fleece auth' for guidance.");
            return 1;
        }

        var ghIssue = await gitHubService.GetIssueAsync(number);

        var branch = gitService.GetCurrentBranch() ?? "(detached HEAD)";

        var issue = await fleeceService.CreateAsync(
            title: ghIssue.Title,
            type: IssueType.Task,
            description: ghIssue.Body,
            tags: [KeyedTag.Create(AbsorbedFromTagKey, number.ToString())]);

        await gitHubService.AddCommentAsync(
            number,
            $"Absorbed into Fleece issue `{issue.Id}` on branch `{branch}`.");

        var login = auth.Login;
        if (!string.IsNullOrWhiteSpace(login))
        {
            await gitHubService.AssignAsync(number, login);
        }

        if (settings.Json)
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
}
