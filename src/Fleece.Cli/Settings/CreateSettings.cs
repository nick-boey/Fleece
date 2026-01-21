using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class CreateSettings : CommandSettings
{
    [CommandOption("-t|--title <TITLE>")]
    [Description("Issue title (required)")]
    public string Title { get; init; } = null!;

    [CommandOption("--type <TYPE>")]
    [Description("Issue type: task, bug, chore, idea, feature (required)")]
    public string Type { get; init; } = null!;

    [CommandOption("-d|--description <DESC>")]
    [Description("Issue description")]
    public string? Description { get; init; }

    [CommandOption("-s|--status <STATUS>")]
    [Description("Initial status (default: open)")]
    public string? Status { get; init; }

    [CommandOption("-p|--priority <PRIORITY>")]
    [Description("Priority 1-5 (1=highest)")]
    public int? Priority { get; init; }

    [CommandOption("--linked-pr <PR>")]
    [Description("Associated PR number")]
    public int? LinkedPr { get; init; }

    [CommandOption("--linked-issues <ISSUES>")]
    [Description("Comma-separated linked issue IDs")]
    public string? LinkedIssues { get; init; }

    [CommandOption("--parent-issues <ISSUES>")]
    [Description("Comma-separated parent issue IDs")]
    public string? ParentIssues { get; init; }

    [CommandOption("-g|--group <GROUP>")]
    [Description("Issue group for categorization")]
    public string? Group { get; init; }

    [CommandOption("-a|--assign <USER>")]
    [Description("Username to assign the issue to")]
    public string? AssignedTo { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }

    [CommandOption("--json-verbose")]
    [Description("Output as JSON with all metadata fields")]
    public bool JsonVerbose { get; init; }
}
