using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// Bring fleece data up to the current schema. Today this converts the legacy hashed
/// <c>.fleece/issues_*.jsonl</c> + <c>.fleece/tombstones_*.jsonl</c> layout into the
/// event-sourced layout via <see cref="IMigrationService"/>. Future schema migrations
/// extend the same pipeline rather than introducing new commands.
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
