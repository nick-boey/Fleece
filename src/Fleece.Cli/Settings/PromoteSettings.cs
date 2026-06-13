using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class PromoteSettings : CommandSettings
{
    [CommandArgument(0, "<IDs>")]
    [Description("One or more Fleece issue IDs to promote into a single GitHub issue")]
    public string[] Ids { get; init; } = [];

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
