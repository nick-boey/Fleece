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
    [Description("New status: open, complete, closed, archived")]
    public string? Status { get; init; }

    [CommandOption("--type <TYPE>")]
    [Description("New type: task, bug, chore, idea, feature")]
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
    [Description("Replace parent issue IDs (comma-separated)")]
    public string? ParentIssues { get; init; }

    [CommandOption("-g|--group <GROUP>")]
    [Description("New issue group")]
    public string? Group { get; init; }

    [CommandOption("-a|--assign <USER>")]
    [Description("New assignee username")]
    public string? AssignedTo { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
