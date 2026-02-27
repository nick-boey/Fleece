using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class ListSettings : FleeceCommandSettings
{
    [CommandOption("-s|--status <STATUS>")]
    [Description("Filter by status: draft, open, progress, review, complete, archived, closed")]
    public string? Status { get; init; }

    [CommandOption("-y|--type <TYPE>")]
    [Description("Filter by type: task, bug, chore, feature, idea")]
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

    [CommandOption("--tag-key <KEY=VALUE>")]
    [Description("Filter by keyed tag (e.g., project=frontend). Can specify multiple times.")]
    public string[]? KeyedTags { get; init; }

    [CommandOption("--linked-pr <PR>")]
    [Description("Filter by linked PR number")]
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
    [Description("Scope tree view to descendants of this issue ID (implies --tree)")]
    public string? TreeRoot { get; init; }

    [CommandOption("--next")]
    [Description("Display as a bottom-up task graph showing approximate task ordering")]
    public bool Next { get; init; }
}
