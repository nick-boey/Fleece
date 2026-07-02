using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class PromoteSettings : CommandSettings
{
    [CommandArgument(0, "<IDs>")]
    [Description("One or more Fleece issue IDs to promote into a single durable issue")]
    public string[] Ids { get; init; } = [];

    [CommandOption("--ref <ref>")]
    [Description("External issue reference (e.g. a Linear identifier or URL) to record; when the tracker is Linear, a bare promote emits the payload and --ref records it")]
    public string? Ref { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
