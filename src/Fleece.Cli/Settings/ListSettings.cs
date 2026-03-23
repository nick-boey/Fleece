using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class ListSettings : FleeceCommandSettings
{
    [CommandArgument(0, "[ID]")]
    [Description("Optional issue ID to show with its hierarchy (parents and children)")]
    public string? IssueId { get; init; }

    [CommandOption("--parents")]
    [Description("When showing a specific issue, only include parent issues (not children)")]
    public bool ParentsOnly { get; init; }

    [CommandOption("--children")]
    [Description("When showing a specific issue, only include child issues (not parents)")]
    public bool ChildrenOnly { get; init; }

    [CommandOption("-s|--status <STATUS>")]
    [Description("Filter by status: draft, open, progress, review, complete, archived, closed")]
    public string? Status { get; init; }

    [CommandOption("-y|--type <TYPE>")]
    [Description("Filter by type: task, bug, chore, feature, idea, verify")]
    public string? Type { get; init; }

    [CommandOption("-p|--priority <PRIORITY>")]
    [Description("Filter by priority")]
    public int? Priority { get; init; }

    [CommandOption("--assigned <USER>")]
    [Description("Filter by assignee")]
    public string? AssignedTo { get; init; }

    [CommandOption("--tag <TAG>")]
    [Description("Filter by tag. Use 'key' to match all issues with that tag key, or 'key=value' for exact match. Can specify multiple times.")]
    public string[]? Tags { get; init; }

    [CommandOption("--linked-pr <PR>")]
    [Description("Filter by linked PR number (checks hsp-linked-pr tags)")]
    public int? LinkedPr { get; init; }

    [CommandOption("--search <QUERY>")]
    [Description("Search using query syntax (e.g., 'status:open type:bug login')")]
    public string? Search { get; init; }

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

    [CommandOption("--sync-status")]
    [Description("Show git sync status for each issue (~=synced, +=committed, *=local)")]
    public bool SyncStatus { get; init; }

    [CommandOption("--tree")]
    [Description("Display issues in a tree view based on parent-child relationships")]
    public bool Tree { get; init; }

    [CommandOption("--tree-root <ID>")]
    [Description("(Deprecated: Use '<id> --children' instead) Scope tree view to descendants of this issue ID (implies --tree)")]
    public string? TreeRoot { get; init; }

    [CommandOption("--next")]
    [Description("Display as a bottom-up task graph showing approximate task ordering")]
    public bool Next { get; init; }

    [CommandOption("--me")]
    [Description("Filter to issues assigned to current user")]
    public bool Me { get; init; }
}
