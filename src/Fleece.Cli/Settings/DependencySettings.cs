using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class DependencySettings : FleeceCommandSettings
{
    [CommandOption("--parent <ID>")]
    [Description("Parent issue ID")]
    public string? ParentId { get; init; }

    [CommandOption("--child <ID>")]
    [Description("Child issue ID")]
    public string? ChildId { get; init; }

    [CommandOption("--remove")]
    [Description("Remove the dependency instead of adding it")]
    public bool Remove { get; init; }

    [CommandOption("--after <ID>")]
    [Description("Place after this sibling in sort order")]
    public string? After { get; init; }

    [CommandOption("--before <ID>")]
    [Description("Place before this sibling in sort order")]
    public string? Before { get; init; }

    [CommandOption("--first")]
    [Description("Place at the beginning of the sort order")]
    public bool First { get; init; }

    [CommandOption("--last")]
    [Description("Place at the end of the sort order")]
    public bool Last { get; init; }

    [CommandOption("--replace")]
    [Description("Replace all existing parents instead of adding to them")]
    public bool Replace { get; init; }

    [CommandOption("--primary")]
    [Description("Make the new parent the primary (first in parent list)")]
    public bool Primary { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(ParentId))
        {
            return ValidationResult.Error("--parent is required");
        }

        if (string.IsNullOrWhiteSpace(ChildId))
        {
            return ValidationResult.Error("--child is required");
        }

        // Count position flags
        var positionFlags = new[] { !string.IsNullOrWhiteSpace(After), !string.IsNullOrWhiteSpace(Before), First, Last }
            .Count(f => f);

        if (positionFlags > 1)
        {
            return ValidationResult.Error("Only one position flag (--after, --before, --first, --last) can be specified");
        }

        if (Remove && positionFlags > 0)
        {
            return ValidationResult.Error("Position flags cannot be used with --remove");
        }

        if (Remove && Replace)
        {
            return ValidationResult.Error("--replace cannot be used with --remove");
        }

        if (Remove && Primary)
        {
            return ValidationResult.Error("--primary cannot be used with --remove");
        }

        return ValidationResult.Success();
    }
}
