using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class EditSettings : CommandSettings
{
    [CommandArgument(0, "<ID>")]
    [Description("Issue ID to edit")]
    public string Id { get; init; } = null!;

    [CommandOption("-t|--title <TITLE>")]
    [Description("New title")]
    public string? Title { get; init; }

    [CommandOption("-d|--description <DESC>")]
    [Description("New description")]
    public string? Description { get; init; }

    [CommandOption("-s|--status <STATUS>")]
    [Description("New status: open, progress, review, complete, archived, closed")]
    public string? Status { get; init; }

    [CommandOption("-y|--type <TYPE>")]
    [Description("New type: task, bug, chore, feature")]
    public string? Type { get; init; }

    [CommandOption("-p|--priority <PRIORITY>")]
    [Description("New priority 1-5")]
    public int? Priority { get; init; }

    [CommandOption("--linked-pr <PR>")]
    [Description("New associated PR number")]
    public int? LinkedPr { get; init; }

    [CommandOption("--linked-issues <ISSUES>")]
    [Description("Replace linked issue IDs (comma-separated)")]
    public string? LinkedIssues { get; init; }

    [CommandOption("--parent-issues <ISSUES>")]
    [Description("Replace parent issue IDs with optional sort order (e.g., abc123,def456:bbb)")]
    public string? ParentIssues { get; init; }

    [CommandOption("-a|--assign <USER>")]
    [Description("New assignee username")]
    public string? AssignedTo { get; init; }

    [CommandOption("--tags <TAGS>")]
    [Description("Replace tags (comma-separated, no whitespace allowed in tags)")]
    public string? Tags { get; init; }

    [CommandOption("-b|--working-branch <BRANCH>")]
    [Description("New working branch ID (valid Git branch name characters only)")]
    public string? WorkingBranchId { get; init; }

    [CommandOption("-e|--execution-mode <MODE>")]
    [Description("Execution mode for child issues: series, parallel")]
    public string? ExecutionMode { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }

    [CommandOption("--json-verbose")]
    [Description("Output as JSON with all metadata fields")]
    public bool JsonVerbose { get; init; }
}
