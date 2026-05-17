using Fleece.Cli.Settings;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class LinkCommand(ILinkService linkService, IAnsiConsole console) : AsyncCommand<LinkSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, LinkSettings settings)
    {
        if (!settings.Merge)
        {
            console.MarkupLine("[red]Error:[/] --merge is required (no other modes are supported yet).");
            return 1;
        }

        var result = await linkService.CreateMergeMarkerAsync();
        if (result.MarkerCreated)
        {
            console.MarkupLine($"[green]{Markup.Escape(result.Message)}[/]");
            console.MarkupLine($"[dim]  Parents: {string.Join(", ", result.Parents)}[/]");
        }
        else
        {
            console.MarkupLine($"[dim]{Markup.Escape(result.Message)}[/]");
        }
        return 0;
    }
}
