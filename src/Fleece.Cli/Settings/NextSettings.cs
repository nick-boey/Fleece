using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class NextSettings : FleeceCommandSettings
{
    [CommandOption("-p|--parent <PARENT>")]
    [Description("Show next issues only under this parent")]
    public string? Parent { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }

    [CommandOption("--json-verbose")]
    [Description("Output as JSON with all metadata fields")]
    public bool JsonVerbose { get; init; }

    [CommandOption("--one-line")]
    [Description("Output each issue on a single line: <id> <status> <group> <type> <title>")]
    public bool OneLine { get; init; }

    [CommandOption("--sort <ORDER>")]
    [Description("Sort order for actionable issues. Comma-separated criteria: created, priority, description, title. Append :asc or :desc (default: created:asc)")]
    public string? Sort { get; init; }
}
