namespace Fleece.Core.Models;

/// <summary>
/// Represents a plan for merging duplicate issues across multiple files.
/// </summary>
public sealed record MergePlan
{
    /// <summary>
    /// Issues that have no duplicates and need no merging.
    /// </summary>
    public required IReadOnlyList<Issue> UniqueIssues { get; init; }

    /// <summary>
    /// Groups of issues sharing the same ID that need to be merged.
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<Issue>> DuplicateGroups { get; init; }

    /// <summary>
    /// The user to attribute merge changes to.
    /// </summary>
    public string? CurrentUser { get; init; }

    /// <summary>
    /// Number of duplicate groups that will be merged.
    /// </summary>
    public int DuplicateCount => DuplicateGroups.Count;
}
