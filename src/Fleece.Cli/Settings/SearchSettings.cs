using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class SearchSettings : FleeceCommandSettings
{
    [CommandArgument(0, "<QUERY>")]
    [Description("Text to search for. Use key:value syntax to search keyed tags (e.g., 'project:frontend')")]
    public string Query { get; init; } = null!;

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }

    [CommandOption("--json-verbose")]
    [Description("Output as JSON with all metadata fields")]
    public bool JsonVerbose { get; init; }
}
