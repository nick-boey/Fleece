using Fleece.Cli.Settings;
using Fleece.Cli.Workflows;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// Reports authentication status for the active durable tracker. Routes to the configured
/// <see cref="ITrackerWorkflow"/>: the GitHub path reports the resolved login + token source
/// (exiting non-zero when unauthenticated); the Linear path reports that the CLI does not
/// authenticate to Linear and exits zero.
/// </summary>
public sealed class AuthCommand(ITrackerWorkflow trackerWorkflow) : AsyncCommand<AuthSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AuthSettings settings)
        => trackerWorkflow.AuthAsync(new AuthContext(settings.Json));
}
