using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class ClearConflictsSettings : CommandSettings
{
    [CommandArgument(0, "<ID>")]
    [Description("Issue ID to clear conflicts for")]
    public string Id { get; init; } = null!;
}
