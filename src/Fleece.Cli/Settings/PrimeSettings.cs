using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class PrimeSettings : CommandSettings
{
    [CommandArgument(0, "[topic]")]
    [Description("Topic to display help for (workflow, hierarchy, commands, types, statuses, sync, json, questions, tips)")]
    public string? Topic { get; init; }
}
