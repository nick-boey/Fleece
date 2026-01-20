using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class HistorySettings : CommandSettings
{
    [CommandArgument(0, "[ISSUE_ID]")]
    [Description("Optional issue ID to filter history")]
    public string? IssueId { get; init; }

    [CommandOption("-u|--user <USER>")]
    [Description("Filter by user name")]
    public string? User { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
