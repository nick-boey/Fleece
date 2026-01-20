using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class MigrateSettings : CommandSettings
{
    [CommandOption("--dry-run")]
    [Description("Check if migration is needed without making changes")]
    public bool DryRun { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
