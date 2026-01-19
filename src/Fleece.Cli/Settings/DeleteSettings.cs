using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class DeleteSettings : CommandSettings
{
    [CommandArgument(0, "<ID>")]
    [Description("Issue ID to delete")]
    public string Id { get; init; } = null!;
}
