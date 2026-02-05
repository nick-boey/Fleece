using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class ListSettings : CommandSettings
{
    [CommandOption("-s|--status <STATUS>")]
    [Description("Filter by status: open, progress, review, complete, archived, closed")]
    public string? Status { get; init; }

    [CommandOption("-y|--type <TYPE>")]
    [Description("Filter by type: task, bug, chore, feature")]
    public string? Type { get; init; }

    [CommandOption("-p|--priority <PRIORITY>")]
    [Description("Filter by priority")]
    public int? Priority { get; init; }

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

    [CommandOption("--json-verbose")]
    [Description("Output as JSON with all metadata fields")]
    public bool JsonVerbose { get; init; }

    [CommandOption("-a|--all")]
    [Description("Show all issues including all statuses")]
    public bool All { get; init; }

    [CommandOption("--one-line")]
    [Description("Output each issue on a single line: <id> <status> <type> <title>")]
    public bool OneLine { get; init; }

    [CommandOption("--strict")]
    [Description("Fail with exit code 1 if schema warnings are detected")]
    public bool Strict { get; init; }
}
