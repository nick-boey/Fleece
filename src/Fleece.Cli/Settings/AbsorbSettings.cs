using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class AbsorbSettings : CommandSettings
{
    [CommandArgument(0, "<reference>")]
    [Description("GitHub issue reference to absorb, e.g. #123 (a leading # is required)")]
    public string Reference { get; init; } = null!;

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
