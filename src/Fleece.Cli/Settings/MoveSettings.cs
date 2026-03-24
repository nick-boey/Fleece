using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class MoveSettings : FleeceCommandSettings
{
    [CommandArgument(0, "<ID>")]
    [Description("Issue ID to move")]
    public string IssueId { get; init; } = null!;

    [CommandOption("--parent <ID>")]
    [Description("Parent issue ID (required if issue has multiple parents)")]
    public string? ParentId { get; init; }

    [CommandOption("--up")]
    [Description("Move the issue up (before its previous sibling)")]
    public bool Up { get; init; }

    [CommandOption("--down")]
    [Description("Move the issue down (after its next sibling)")]
    public bool Down { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }

    public override ValidationResult Validate()
    {
        if (Up && Down)
        {
            return ValidationResult.Error("Only one of --up or --down can be specified");
        }

        if (!Up && !Down)
        {
            return ValidationResult.Error("Either --up or --down must be specified");
        }

        return ValidationResult.Success();
    }
}
