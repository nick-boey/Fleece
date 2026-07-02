using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class InstallSettings : CommandSettings
{
    [CommandOption("--tracker <TRACKER>")]
    [Description("Durable issue tracker to configure: github or linear. When omitted, prompts interactively (TTY) or defaults to github.")]
    public string? Tracker { get; init; }
}
