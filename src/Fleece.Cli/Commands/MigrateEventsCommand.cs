using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// One-shot migration from the legacy hashed file layout to the event-sourced layout.
/// Distinct from the older <see cref="MigrateCommand"/> which migrated within the
/// legacy shape itself.
/// </summary>
public sealed class MigrateEventsCommand(
    IEventMigrationService migration,
    IGitConfigService gitConfig,
    IAnsiConsole console)
    : AsyncCommand<MigrateEventsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, MigrateEventsSettings settings)
    {
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
            console.MarkupLine("[green]No legacy hashed files found; nothing to migrate.[/]");
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
