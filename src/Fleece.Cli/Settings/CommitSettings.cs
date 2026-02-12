using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class CommitSettings : CommandSettings
{
    [CommandOption("-m|--message <MESSAGE>")]
    [Description("Commit message (default: 'Update fleece issues')")]
    public string? Message { get; init; }

    [CommandOption("--ci")]
    [Description("Allow CI to run (omits [skip ci] from commit message)")]
    public bool Ci { get; init; }

    [CommandOption("--push")]
    [Description("Push to remote after committing")]
    public bool Push { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
