using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class CleanSettings : CommandSettings
{
    [CommandOption("--include-complete")]
    [Description("Also clean issues with Complete status")]
    public bool IncludeComplete { get; init; }

    [CommandOption("--include-closed")]
    [Description("Also clean issues with Closed status")]
    public bool IncludeClosed { get; init; }

    [CommandOption("--include-archived")]
    [Description("Also clean issues with Archived status")]
    public bool IncludeArchived { get; init; }

    [CommandOption("--no-strip-refs")]
    [Description("Do not strip references to cleaned issues from remaining issues")]
    public bool NoStripRefs { get; init; }

    [CommandOption("--dry-run")]
    [Description("Show what would be cleaned without making changes")]
    public bool DryRun { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
