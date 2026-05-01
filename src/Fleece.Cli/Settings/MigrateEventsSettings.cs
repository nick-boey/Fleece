using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class MigrateEventsSettings : CommandSettings
{
    [CommandOption("--json")]
    [Description("Output a JSON summary of the migration")]
    public bool Json { get; init; }
}
