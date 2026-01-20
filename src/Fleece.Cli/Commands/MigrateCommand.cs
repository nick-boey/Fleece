using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class MigrateCommand(IMigrationService migrationService) : AsyncCommand<MigrateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, MigrateSettings settings)
    {
        if (settings.DryRun)
        {
            var needed = await migrationService.IsMigrationNeededAsync();

            if (settings.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { migrationNeeded = needed }));
            }
            else if (needed)
            {
                AnsiConsole.MarkupLine("[yellow]Migration is needed. Run 'fleece migrate' to migrate issues.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[green]No migration needed. All issues have property timestamps.[/]");
            }

            return 0;
        }

        var result = await migrationService.MigrateAsync();

        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                totalIssues = result.TotalIssues,
                migratedIssues = result.MigratedIssues,
                alreadyMigratedIssues = result.AlreadyMigratedIssues,
                wasMigrationNeeded = result.WasMigrationNeeded
            }));
        }
        else if (result.WasMigrationNeeded)
        {
            AnsiConsole.MarkupLine($"[green]Migration complete![/]");
            AnsiConsole.MarkupLine($"  Total issues: {result.TotalIssues}");
            AnsiConsole.MarkupLine($"  Migrated: {result.MigratedIssues}");
            AnsiConsole.MarkupLine($"  Already migrated: {result.AlreadyMigratedIssues}");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]No migration needed. All issues have property timestamps.[/]");
        }

        return 0;
    }
}
