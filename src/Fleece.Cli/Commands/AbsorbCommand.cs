using Fleece.Cli.Settings;
using Fleece.Cli.Workflows;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// Pulls a durable issue into Fleece. Routes to the configured <see cref="ITrackerWorkflow"/>: the
/// GitHub path converts a <c>#&lt;github-#&gt;</c> issue and comments on/assigns it without closing;
/// the Linear path prints guidance (<c>fleece create ... --tag absorbed-from=&lt;ref&gt;</c>) and
/// changes no state.
/// </summary>
public sealed class AbsorbCommand(ITrackerWorkflow trackerWorkflow) : AsyncCommand<AbsorbSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AbsorbSettings settings)
        => trackerWorkflow.AbsorbAsync(new AbsorbContext(settings.Reference, settings.Json));
}
