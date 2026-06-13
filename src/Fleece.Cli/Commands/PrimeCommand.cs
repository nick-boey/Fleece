using System.IO.Abstractions;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// The SessionStart hook. v4 splits Fleece's onboarding across three surfaces: the durable
/// philosophy lives in the CLAUDE.md memory block, the full static reference lives in the
/// installed `fleece` skill (.claude/skills/fleece/), and this command emits ONLY the dynamic
/// state — the live count of active issues — so an agent is armed with the CI-gate tripwire it
/// could not otherwise pull from a skill. A clean branch emits nothing (~0 tokens/session).
/// </summary>
public sealed class PrimeCommand(
    IFleeceService fleece,
    IFileSystem fileSystem,
    BasePathProvider basePath,
    IAnsiConsole console)
    : AsyncCommand<PrimeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, PrimeSettings settings)
    {
        // Check if .fleece folder exists - if not, exit silently (no priming needed)
        var fleeceDirectoryPath = fileSystem.Path.Combine(basePath.BasePath, ".fleece");
        if (!fileSystem.Directory.Exists(fleeceDirectoryPath))
        {
            return 0;
        }

        var activeCount = await CountActiveIssuesAsync();
        if (activeCount == 0)
        {
            // Clean branch: nothing actionable, emit nothing.
            return 0;
        }

        console.WriteLine(BuildActiveIssueSignal(activeCount));
        return 0;
    }

    private async Task<int> CountActiveIssuesAsync()
    {
        var all = await fleece.GetAllAsync();
        return all.Count(i => i.Status.IsActive());
    }

    private static string BuildActiveIssueSignal(int activeCount) => $$"""
        Fleece: {{activeCount}} active issue(s) ({open, progress, review}) on this branch.
        Resolve (complete/closed), promote, or seal them before opening a PR — the CI gate
        blocks merges while any live issue remains under .fleece/issues/. For commands and
        workflow, use the `fleece` skill (.claude/skills/fleece/).
        """;
}
