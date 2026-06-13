using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// "Finish the branch": archives the issue set to <c>.fleece/archive/issues_&lt;contenthash&gt;.jsonl</c>
/// and clears the live <c>.fleece/issues/</c> directory, but only when every issue is inactive.
/// Refuses (and lists the offending active issues) while any issue is still
/// <c>Open</c>/<c>Progress</c>/<c>Review</c>.
/// </summary>
public sealed class SealCommand(ISealService sealService, IAnsiConsole console) : AsyncCommand<SealSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SealSettings settings)
    {
        var result = await sealService.SealAsync();

        if (settings.Json)
        {
            console.WriteLine(JsonSerializer.Serialize(new
            {
                @sealed = result.Sealed,
                archivePath = result.ArchivePath,
                removedCount = result.RemovedCount,
                activeIssues = result.ActiveIssues
                    .Select(i => new { id = i.Id, title = i.Title, status = i.Status.ToString() })
                    .ToArray(),
            }, new JsonSerializerOptions { WriteIndented = false }));

            return result.Sealed ? 0 : 1;
        }

        if (!result.Sealed)
        {
            console.MarkupLine("[red]Cannot seal: active issues remain.[/]");
            foreach (var issue in result.ActiveIssues)
            {
                console.MarkupLine(
                    $"  [yellow]{issue.Id}[/] {Markup.Escape(issue.Title)} [grey]({issue.Status})[/]");
            }
            console.MarkupLine(
                "[grey]Move each issue to Complete, Closed, or Promoted before sealing.[/]");
            return 1;
        }

        if (result.ArchivePath is null)
        {
            console.MarkupLine("[green]Nothing to seal: no live issues.[/]");
            return 0;
        }

        console.MarkupLine("[green]Branch sealed.[/]");
        console.MarkupLine($"  Archive: {Markup.Escape(result.ArchivePath)}");
        console.MarkupLine($"  Issues archived and cleared: {result.RemovedCount}");
        return 0;
    }
}
