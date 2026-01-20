using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class HistoryCommand(IChangeService changeService) : AsyncCommand<HistorySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, HistorySettings settings)
    {
        var changes = await GetChangesAsync(settings);

        if (settings.Json)
        {
            JsonFormatter.RenderChanges(changes);
        }
        else
        {
            TableFormatter.RenderChanges(changes);
        }

        return 0;
    }

    private async Task<IReadOnlyList<Fleece.Core.Models.ChangeRecord>> GetChangesAsync(HistorySettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.IssueId))
        {
            return await changeService.GetByIssueIdAsync(settings.IssueId);
        }

        if (!string.IsNullOrWhiteSpace(settings.User))
        {
            return await changeService.GetByUserAsync(settings.User);
        }

        return await changeService.GetAllAsync();
    }
}
