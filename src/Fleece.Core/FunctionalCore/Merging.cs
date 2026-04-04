using Fleece.Core.Models;
using Fleece.Core.Services;

namespace Fleece.Core.FunctionalCore;

/// <summary>
/// Pure functions for planning and applying issue merges across multiple files.
/// </summary>
public static class Merging
{
    /// <summary>
    /// Identifies duplicate issues across file groups and returns a merge plan.
    /// </summary>
    public static MergePlan Plan(
        IReadOnlyList<(string filePath, IReadOnlyList<Issue> issues)> fileGroups,
        string? currentUser = null)
    {
        var allIssues = fileGroups.SelectMany(fg => fg.issues).ToList();
        var grouped = allIssues.GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase);

        var uniqueIssues = new List<Issue>();
        var duplicateGroups = new List<IReadOnlyList<Issue>>();

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

        return new MergePlan
        {
            UniqueIssues = uniqueIssues,
            DuplicateGroups = duplicateGroups,
            CurrentUser = currentUser
        };
    }

    /// <summary>
    /// Applies the merge plan, returning the consolidated issue list after property-level merges.
    /// </summary>
    public static IReadOnlyList<Issue> Apply(MergePlan plan)
    {
        var merger = new IssueMerger();
        var result = new List<Issue>(plan.UniqueIssues);

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
