using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class DiffSettings : CommandSettings
{
    [CommandArgument(0, "[FILE1]")]
    [Description("First JSONL file to compare (or issue ID when showing history)")]
    public string? File1 { get; init; }

    [CommandArgument(1, "[FILE2]")]
    [Description("Second JSONL file to compare")]
    public string? File2 { get; init; }

    [CommandOption("-u|--user <USER>")]
    [Description("Filter history by user name")]
    public string? User { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
