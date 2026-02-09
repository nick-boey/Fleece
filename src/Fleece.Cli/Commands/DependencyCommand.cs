using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class DependencyCommand(IDependencyService dependencyService, IStorageService storageService)
    : AsyncCommand<DependencySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DependencySettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        try
        {
            Issue result;

            if (settings.Remove)
            {
                result = await dependencyService.RemoveDependencyAsync(
                    settings.ParentId!, settings.ChildId!);

                if (settings.Json)
                {
                    JsonFormatter.RenderIssue(result);
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        $"[green]Removed[/] [bold]{settings.ParentId}[/] as parent of [bold]{settings.ChildId}[/]");
                    TableFormatter.RenderIssue(result);
                }
            }
            else
            {
                var position = MapPosition(settings);

                result = await dependencyService.AddDependencyAsync(
                    settings.ParentId!, settings.ChildId!, position);

                if (settings.Json)
                {
                    JsonFormatter.RenderIssue(result);
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        $"[green]Added[/] [bold]{settings.ParentId}[/] as parent of [bold]{settings.ChildId}[/]");
                    TableFormatter.RenderIssue(result);
                }
            }

            return 0;
        }
        catch (KeyNotFoundException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
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
