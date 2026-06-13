using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// Reports GitHub authentication status (resolved login + token source). Exits non-zero when no
/// usable credentials are found.
/// </summary>
public sealed class AuthCommand(IGitHubService gitHubService, IAnsiConsole console) : AsyncCommand<AuthSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AuthSettings settings)
    {
        var auth = await gitHubService.ResolveAuthAsync();

        if (settings.Json)
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
