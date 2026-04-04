using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class DeleteCommand(IFleeceService fleeceService, ISettingsService settingsService, IGitConfigService gitConfigService) : AsyncCommand<DeleteSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeleteSettings settings)
    {
        IFleeceService fleece = fleeceService;
        if (!string.IsNullOrWhiteSpace(settings.IssuesFile))
        {
            fleece = FleeceService.ForFile(settings.IssuesFile, settingsService, gitConfigService);
        }

        var (hasMultiple, message) = await fleece.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        var matches = await fleece.ResolveByPartialIdAsync(settings.Id);

        if (matches.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
            return 1;
        }

        if (matches.Count > 1)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.Id}':");
            TableFormatter.RenderIssues(matches);
            return 1;
        }

        var resolvedId = matches[0].Id;
        var deleted = await fleece.DeleteAsync(resolvedId);

        if (deleted)
        {
            AnsiConsole.MarkupLine($"[green]Deleted issue[/] [bold]{resolvedId}[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{resolvedId}' not found");
            return 1;
        }
    }
}
