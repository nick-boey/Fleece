using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class ProjectSettings : CommandSettings
{
    [CommandOption("--json")]
    [Description("Output a JSON summary of the projection")]
    public bool Json { get; init; }
}
