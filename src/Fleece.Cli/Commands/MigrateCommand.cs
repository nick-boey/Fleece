using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// One-time bring-forward of a legacy hashed-file repository into the current Fleece
/// storage layout. Converts the legacy hashed <c>.fleece/issues_*.jsonl</c> +
/// <c>.fleece/tombstones_*.jsonl</c> files via <see cref="IMigrationService"/>. All
/// actively maintained repositories are already on the current layout, so this is a
/// no-op there. It is idempotent: a second run reports "no migration needed".
///
/// This is NOT the path for moving long-running issues to GitHub Issues — see the
/// <c>fleece</c> skill's <c>references/v4-migration.md</c> and use <c>fleece promote</c> for that.
/// </summary>
public sealed class MigrateCommand(
    IMigrationService migration,
    IGitConfigService gitConfig,
    IAnsiConsole console)
    : AsyncCommand<MigrateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, MigrateSettings settings)
    {
        if (settings.DryRun)
        {
            var needed = await migration.IsMigrationNeededAsync();

            if (settings.Json)
            {
                console.WriteLine(JsonSerializer.Serialize(new { migrationNeeded = needed }));
            }
            else if (needed)
            {
                console.MarkupLine("[yellow]Migration is needed. Run 'fleece migrate' to migrate issues.[/]");
            }
            else
            {
                console.MarkupLine("[green]No migration needed. All issues are up to date.[/]");
            }

            return 0;
        }

        var by = gitConfig.GetUserName() ?? Environment.UserName;
        var result = await migration.MigrateAsync(by);

        if (settings.Json)
        {
            console.WriteLine(JsonSerializer.Serialize(new
            {
                wasMigrationNeeded = result.WasMigrationNeeded,
                legacyIssueFiles = result.LegacyIssueFilesConsumed,
                legacyTombstoneFiles = result.LegacyTombstoneFilesConsumed,
                issuesWritten = result.IssuesWritten,
                tombstonesWritten = result.TombstonesWritten,
                gitignoreEntriesAdded = result.GitignoreEntriesAdded.ToArray(),
            }, new JsonSerializerOptions { WriteIndented = false }));
            return 0;
        }

        if (!result.WasMigrationNeeded)
        {
            console.MarkupLine("[green]No migration needed. All issues are up to date.[/]");
            return 0;
        }

        console.MarkupLine("[green]Migration complete.[/]");
        console.MarkupLine($"  Legacy issue files consumed: {result.LegacyIssueFilesConsumed}");
        console.MarkupLine($"  Legacy tombstone files consumed: {result.LegacyTombstoneFilesConsumed}");
        console.MarkupLine($"  Issues written: {result.IssuesWritten}");
        console.MarkupLine($"  Tombstones written: {result.TombstonesWritten}");
        if (result.GitignoreEntriesAdded.Count > 0)
        {
            console.MarkupLine($"  .gitignore entries added: {string.Join(", ", result.GitignoreEntriesAdded)}");
        }
        return 0;
    }
}
