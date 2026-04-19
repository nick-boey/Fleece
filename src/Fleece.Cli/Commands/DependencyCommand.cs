using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class DependencyCommand(IFleeceService fleeceService, IAnsiConsole console)
    : AsyncCommand<DependencySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DependencySettings settings)
    {
        var (hasMultiple, message) = await fleeceService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            console.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        try
        {
            Issue result;

            if (settings.Remove)
            {
                result = await fleeceService.RemoveDependencyAsync(
                    settings.ParentId!, settings.ChildId!);

                if (settings.Json)
                {
                    JsonFormatter.RenderIssue(result);
                }
                else
                {
                    console.MarkupLine(
                        $"[green]Removed[/] [bold]{settings.ParentId}[/] as parent of [bold]{settings.ChildId}[/]");
                    TableFormatter.RenderIssue(console, result);
                }
            }
            else
            {
                var position = MapPosition(settings);

                result = await fleeceService.AddDependencyAsync(
                    settings.ParentId!,
                    settings.ChildId!,
                    position,
                    replaceExisting: settings.Replace,
                    makePrimary: settings.Primary);

                if (settings.Json)
                {
                    JsonFormatter.RenderIssue(result);
                }
                else
                {
                    var action = settings.Replace ? "Set" : "Added";
                    console.MarkupLine(
                        $"[green]{action}[/] [bold]{settings.ParentId}[/] as parent of [bold]{settings.ChildId}[/]");
                    TableFormatter.RenderIssue(console, result);
                }
            }

            return 0;
        }
        catch (KeyNotFoundException ex)
        {
            console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static DependencyPosition MapPosition(DependencySettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.After))
        {
            return new DependencyPosition
            {
                Kind = DependencyPositionKind.After,
                SiblingId = settings.After
            };
        }

        if (!string.IsNullOrWhiteSpace(settings.Before))
        {
            return new DependencyPosition
            {
                Kind = DependencyPositionKind.Before,
                SiblingId = settings.Before
            };
        }

        if (settings.First)
        {
            return new DependencyPosition { Kind = DependencyPositionKind.First };
        }

        // Default is Last
        return new DependencyPosition { Kind = DependencyPositionKind.Last };
    }
}
