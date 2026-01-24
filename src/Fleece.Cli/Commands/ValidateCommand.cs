using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class ValidateCommand(IValidationService validationService, IStorageService storageService)
    : AsyncCommand<ValidateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ValidateSettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        if (!settings.Json)
        {
            AnsiConsole.MarkupLine("[dim]Validating issue dependencies...[/]");
            AnsiConsole.WriteLine();
        }

        var result = await validationService.ValidateDependencyCyclesAsync();

        if (settings.Json)
        {
            RenderJson(result);
        }
        else
        {
            RenderText(result);
        }

        return result.IsValid ? 0 : 1;
    }

    private static void RenderJson(DependencyValidationResult result)
    {
        var output = new
        {
            valid = result.IsValid,
            cycleCount = result.Cycles.Count,
            cycles = result.Cycles.Select(c => new { issues = c.IssueIds }).ToArray()
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        AnsiConsole.WriteLine(JsonSerializer.Serialize(output, options));
    }

    private static void RenderText(DependencyValidationResult result)
    {
        if (result.IsValid)
        {
            AnsiConsole.MarkupLine("[green]No cycles detected. All dependencies are valid.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[red]Found {result.Cycles.Count} cycle(s):[/]");
        AnsiConsole.WriteLine();

        for (int i = 0; i < result.Cycles.Count; i++)
        {
            var cycle = result.Cycles[i];
            var cycleString = string.Join(" → ", cycle.IssueIds);
            AnsiConsole.MarkupLine($"  [yellow]Cycle {i + 1}:[/] {cycleString}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[red]Validation failed:[/] {result.Cycles.Count} cycle(s) detected");
    }
}
