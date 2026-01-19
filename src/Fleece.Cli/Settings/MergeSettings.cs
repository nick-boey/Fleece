using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class MergeSettings : CommandSettings
{
    [CommandOption("--dry-run")]
    [Description("Show what would be merged without making changes")]
    public bool DryRun { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
