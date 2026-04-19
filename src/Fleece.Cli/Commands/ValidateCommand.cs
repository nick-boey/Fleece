using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class ValidateCommand(IFleeceService fleeceService, IAnsiConsole console)
    : AsyncCommand<ValidateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ValidateSettings settings)
    {
        var (hasMultiple, message) = await fleeceService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            console.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        if (!settings.Json)
        {
            console.MarkupLine("[dim]Validating issue dependencies...[/]");
            console.WriteLine();
        }

        var result = await fleeceService.ValidateDependenciesAsync();

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

    private void RenderJson(DependencyValidationResult result)
    {
        var output = new
        {
            valid = result.IsValid,
            cycleCount = result.Cycles.Count,
            cycles = result.Cycles.Select(c => new { issues = c.IssueIds }).ToArray()
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        console.WriteLine(JsonSerializer.Serialize(output, options));
    }

    private void RenderText(DependencyValidationResult result)
    {
        if (result.IsValid)
        {
            console.MarkupLine("[green]No cycles detected. All dependencies are valid.[/]");
            return;
        }

        console.MarkupLine($"[red]Found {result.Cycles.Count} cycle(s):[/]");
        console.WriteLine();

        for (int i = 0; i < result.Cycles.Count; i++)
        {
            var cycle = result.Cycles[i];
            var cycleString = string.Join(" → ", cycle.IssueIds);
            console.MarkupLine($"  [yellow]Cycle {i + 1}:[/] {cycleString}");
        }

        console.WriteLine();
        console.MarkupLine($"[red]Validation failed:[/] {result.Cycles.Count} cycle(s) detected");
    }
}
