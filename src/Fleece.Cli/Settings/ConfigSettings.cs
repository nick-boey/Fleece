using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class ConfigSettings : CommandSettings
{
    [CommandOption("-l|--list")]
    [Description("List all settings with their values and sources")]
    public bool List { get; init; }

    [CommandOption("-g|--global")]
    [Description("Operate on global settings (~/.fleece/settings.json) instead of local")]
    public bool Global { get; init; }

    [CommandOption("--set <KEY_VALUE>")]
    [Description("Set a configuration value (format: key=value). Use empty value to clear.")]
    public string? Set { get; init; }

    [CommandOption("--get <KEY>")]
    [Description("Get a configuration value by key")]
    public string? Get { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }

    [CommandOption("--path")]
    [Description("Show the path to the settings file")]
    public bool Path { get; init; }
}
