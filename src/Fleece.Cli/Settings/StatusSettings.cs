using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class StatusSettings : FleeceCommandSettings
{
    [CommandArgument(0, "<IDs>")]
    [Description("One or more issue IDs to update")]
    public string[] Ids { get; init; } = [];

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }

    [CommandOption("--json-verbose")]
    [Description("Output as JSON with all metadata fields")]
    public bool JsonVerbose { get; init; }
}
