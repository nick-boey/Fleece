using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class OpenSpecDependenciesSettings : CommandSettings
{
    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
