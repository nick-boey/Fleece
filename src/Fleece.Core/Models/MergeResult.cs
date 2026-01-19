namespace Fleece.Core.Models;

/// <summary>
/// Represents the result of merging two issues.
/// </summary>
public sealed record MergeResult
{
    public required Issue MergedIssue { get; init; }
    public required IReadOnlyList<PropertyConflict> PropertyConflicts { get; init; }
    public bool HadConflicts => PropertyConflicts.Count > 0;
}
