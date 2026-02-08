using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class PrimeSettings : CommandSettings
{
    [CommandArgument(0, "[topic]")]
    [Description("Topic to display help for (hierarchy, commands, statuses, sync, json, questions, next, tree)")]
    public string? Topic { get; init; }
}
