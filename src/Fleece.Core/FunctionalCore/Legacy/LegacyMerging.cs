using Fleece.Core.Models.Legacy;
using Fleece.Core.Services.Legacy;

namespace Fleece.Core.FunctionalCore.Legacy;

/// <summary>
/// Pure functions for planning and applying legacy issue merges across multiple files.
/// Replaced by event-replay merging in event-sourced storage.
/// </summary>
public static class LegacyMerging
{
    /// <summary>
    /// Identifies duplicate legacy issues across file groups and returns a merge plan.
    /// </summary>
    public static LegacyMergePlan Plan(
        IReadOnlyList<(string filePath, IReadOnlyList<LegacyIssue> issues)> fileGroups,
        string? currentUser = null)
    {
        var allIssues = fileGroups.SelectMany(fg => fg.issues).ToList();
        var grouped = allIssues.GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase);

        var uniqueIssues = new List<LegacyIssue>();
        var duplicateGroups = new List<IReadOnlyList<LegacyIssue>>();

        foreach (var group in grouped)
        {
            var versions = group.ToList();
            if (versions.Count > 1)
            {
                duplicateGroups.Add(versions);
            }
            else
            {
                uniqueIssues.Add(versions[0]);
            }
        }

        return new LegacyMergePlan
        {
            UniqueIssues = uniqueIssues,
            DuplicateGroups = duplicateGroups,
            CurrentUser = currentUser
        };
    }

    /// <summary>
    /// Applies the legacy merge plan, returning the consolidated issue list after property-level merges.
    /// </summary>
    public static IReadOnlyList<LegacyIssue> Apply(LegacyMergePlan plan)
    {
        var merger = new LegacyIssueMerger();
        var result = new List<LegacyIssue>(plan.UniqueIssues);

        foreach (var group in plan.DuplicateGroups)
        {
            var merged = group[0];
            for (var i = 1; i < group.Count; i++)
            {
                var mergeResult = merger.Merge(merged, group[i], plan.CurrentUser);
                merged = mergeResult.MergedIssue;
            }

            result.Add(merged);
        }

        return result;
    }
}

/// <summary>
/// Plan for merging duplicate legacy issues across multiple files.
/// </summary>
public sealed record LegacyMergePlan
{
    public required IReadOnlyList<LegacyIssue> UniqueIssues { get; init; }
    public required IReadOnlyList<IReadOnlyList<LegacyIssue>> DuplicateGroups { get; init; }
    public string? CurrentUser { get; init; }
    public int DuplicateCount => DuplicateGroups.Count;
}
