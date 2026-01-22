using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class TreeSettings : CommandSettings
{
    [CommandOption("-s|--status <STATUS>")]
    [Description("Filter by status: open, complete, closed, archived")]
    public string? Status { get; init; }

    [CommandOption("-t|--type <TYPE>")]
    [Description("Filter by type: task, bug, chore, idea, feature")]
    public string? Type { get; init; }

    [CommandOption("-p|--priority <PRIORITY>")]
    [Description("Filter by priority")]
    public int? Priority { get; init; }

    [CommandOption("-g|--group <GROUP>")]
    [Description("Filter by group")]
    public string? Group { get; init; }

    [CommandOption("--assigned <USER>")]
    [Description("Filter by assignee")]
    public string? AssignedTo { get; init; }

    [CommandOption("--tag <TAG>")]
    [Description("Filter by tag (can specify multiple times)")]
    public string[]? Tags { get; init; }

    [CommandOption("--linked-pr <PR>")]
    [Description("Filter by linked PR number")]
    public int? LinkedPr { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }

    [CommandOption("-a|--all")]
    [Description("Show all issues including complete, closed, and archived")]
    public bool All { get; init; }
}
