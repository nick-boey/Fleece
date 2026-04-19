using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class DeleteCommand(IFleeceService fleeceService, ISettingsService settingsService, IGitConfigService gitConfigService, IAnsiConsole console) : AsyncCommand<DeleteSettings>
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
            console.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        var matches = await fleece.ResolveByPartialIdAsync(settings.Id);

        if (matches.Count == 0)
        {
            console.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
            return 1;
        }

        if (matches.Count > 1)
        {
            console.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.Id}':");
            TableFormatter.RenderIssues(console, matches);
            return 1;
        }

        var resolvedId = matches[0].Id;
        var deleted = await fleece.DeleteAsync(resolvedId);

        if (deleted)
        {
            console.MarkupLine($"[green]Deleted issue[/] [bold]{resolvedId}[/]");
            return 0;
        }
        else
        {
            console.MarkupLine($"[red]Error:[/] Issue '{resolvedId}' not found");
            return 1;
        }
    }
}
