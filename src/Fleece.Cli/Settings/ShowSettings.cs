using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class ShowSettings : CommandSettings
{
    [CommandArgument(0, "<ID>")]
    [Description("Issue ID to display")]
    public string Id { get; init; } = null!;

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }

    [CommandOption("--json-verbose")]
    [Description("Output as JSON with all metadata fields")]
    public bool JsonVerbose { get; init; }
}
