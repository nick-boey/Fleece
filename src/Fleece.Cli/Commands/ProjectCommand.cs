using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// Compacts the event-sourced state into a fresh snapshot. Refuses to run on
/// any branch other than the configured default branch (typically <c>main</c>).
/// </summary>
public sealed class ProjectCommand(
    IProjectionService projectionService,
    IGitService gitService,
    IGitConfigService gitConfig,
    ISettingsService settingsService,
    IAnsiConsole console)
    : AsyncCommand<ProjectSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ProjectSettings settings)
    {
        var effective = await settingsService.GetEffectiveSettingsAsync();
        var defaultBranch = effective.DefaultBranch;
        var currentBranch = gitService.GetCurrentBranch();

        if (currentBranch is null)
        {
            console.MarkupLine("[red]fleece project must be run inside a git repository.[/]");
            return 1;
        }

        if (!string.Equals(currentBranch, defaultBranch, StringComparison.Ordinal))
        {
            console.MarkupLine(
                $"[red]fleece project may only run on the default branch ('{defaultBranch}'); current branch is '{currentBranch}'.[/]");
            console.MarkupLine(
                "[dim]Tip: schedule the daily GitHub Action installed by `fleece install` to run projection automatically.[/]");
            return 1;
        }

        var actor = gitConfig.GetUserName() ?? Environment.UserName;
        var result = await projectionService.ProjectAsync(DateTimeOffset.UtcNow, actor);

        // Stage the projection's output so the user (or CI) can commit it.
        if (gitService.IsGitRepository())
        {
            var stage = gitService.StageFleeceDirectory();
            if (!stage.Success)
            {
                console.MarkupLine($"[yellow]warning: could not stage .fleece changes: {stage.ErrorMessage}[/]");
            }
        }

        if (settings.Json)
        {
            var payload = new
            {
                issueCount = result.IssueCount,
                changeFilesCompacted = result.ChangeFilesCompacted,
                autoCleaned = result.AutoCleanedTombstones.Select(t => new
                {
                    id = t.IssueId,
                    title = t.OriginalTitle,
                    cleanedAt = t.CleanedAt,
                }).ToArray(),
            };
            console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = false,
            }));
            return 0;
        }

        console.MarkupLine($"[green]Projected {result.ChangeFilesCompacted} change file(s) into {result.IssueCount} issue(s).[/]");
        if (result.AutoCleanedTombstones.Count > 0)
        {
            console.MarkupLine($"[yellow]Auto-cleaned {result.AutoCleanedTombstones.Count} soft-deleted issue(s) older than 30 days.[/]");
        }
        return 0;
    }
}
