using Fleece.Core.Models;
using Fleece.Core.Utilities;

namespace Fleece.Core.FunctionalCore;

/// <summary>
/// Pure functions for dependency management logic.
/// All methods are side-effect free and return modified copies of issues.
/// </summary>
public static class Dependencies
{
    /// <summary>
    /// Adds a parent dependency to a child issue. Returns the updated child issue.
    /// Cycle detection should be performed by the caller before invoking this method.
    /// </summary>
    /// <param name="child">The child issue to add the parent to.</param>
    /// <param name="parentId">The resolved parent issue ID.</param>
    /// <param name="allIssues">All issues for sibling computation.</param>
    /// <param name="position">Sort order positioning for the child within the parent's children.</param>
    /// <param name="replaceExisting">If true, replace all existing parents instead of adding to them.</param>
    /// <param name="makePrimary">If true, make the new parent the primary (first in parent list).</param>
    /// <returns>The updated child issue with the new parent reference.</returns>
    public static Issue AddDependency(
        Issue child,
        string parentId,
        IReadOnlyList<Issue> allIssues,
        DependencyPosition? position = null,
        bool replaceExisting = false,
        bool makePrimary = false)
    {
        position ??= new DependencyPosition();

        // Check if parent already exists as inactive — if so, reactivate it
        var existingInactive = child.ParentIssues.FirstOrDefault(p =>
            string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase) && !p.Active);

        // Check relationship doesn't already exist as active (only when not replacing)
        if (!replaceExisting && existingInactive is null && child.ParentIssues.Any(p =>
            string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase) && p.Active))
        {
            throw new InvalidOperationException(
                $"Issue '{child.Id}' is already a child of '{parentId}'");
        }

        // Compute sort order based on position
        var sortOrder = ComputeSortOrder(parentId, position, allIssues);

        // Build new parent issues list
        List<ParentIssueRef> newParentIssues;

        if (replaceExisting)
        {
            newParentIssues = new List<ParentIssueRef>();
        }
        else if (existingInactive is not null)
        {
            // Reactivate the inactive parent with new sort order
            newParentIssues = child.ParentIssues.Select(p =>
                string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase)
                    ? p with { SortOrder = sortOrder, Active = true }
                    : p).ToList();
            return child with { ParentIssues = newParentIssues };
        }
        else
        {
            newParentIssues = child.ParentIssues.ToList();
        }

        var newParentRef = new ParentIssueRef
        {
            ParentIssue = parentId,
            SortOrder = sortOrder
        };

        if (makePrimary)
        {
            newParentIssues.Insert(0, newParentRef);
        }
        else
        {
            newParentIssues.Add(newParentRef);
        }

        return child with { ParentIssues = newParentIssues };
    }

    /// <summary>
    /// Removes a parent dependency from a child issue. Returns the updated child issue.
    /// </summary>
    /// <param name="child">The child issue to remove the parent from.</param>
    /// <param name="parentId">The resolved parent issue ID to remove.</param>
    /// <returns>The updated child issue without the parent reference.</returns>
    public static Issue RemoveDependency(Issue child, string parentId)
    {
        var existingRef = child.ParentIssues.FirstOrDefault(p =>
            string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase) && p.Active);

        if (existingRef is null)
        {
            throw new InvalidOperationException(
                $"Issue '{child.Id}' is not a child of '{parentId}'");
        }

        // Soft-delete: set Active = false instead of removing
        var newParentIssues = child.ParentIssues.Select(p =>
            string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase)
                ? p with { Active = false }
                : p).ToList();

        return child with { ParentIssues = newParentIssues };
    }

    /// <summary>
    /// Moves a child issue up (before its previous sibling) within a parent's children.
    /// Returns the updated child and any siblings modified during normalization.
    /// </summary>
    public static (Issue moved, IReadOnlyList<Issue> siblings) MoveUp(
        Issue child,
        string parentId,
        IReadOnlyList<Issue> allIssues)
    {
        return Move(child, parentId, allIssues, MoveDirection.Up);
    }

    /// <summary>
    /// Moves a child issue down (after its next sibling) within a parent's children.
    /// Returns the updated child and any siblings modified during normalization.
    /// </summary>
    public static (Issue moved, IReadOnlyList<Issue> siblings) MoveDown(
        Issue child,
        string parentId,
        IReadOnlyList<Issue> allIssues)
    {
        return Move(child, parentId, allIssues, MoveDirection.Down);
    }

    /// <summary>
    /// Checks if adding parentId as a parent of childId would create a cycle.
    /// Pure BFS over the provided issues.
    /// </summary>
    internal static bool WouldCreateCycle(
        string parentId,
        string childId,
        IReadOnlyList<Issue> allIssues)
    {
        if (string.Equals(parentId, childId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parentMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in allIssues)
        {
            parentMap[issue.Id] = (issue.ActiveParentIssues ?? []).Select(p => p.ParentIssue).ToList();
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(parentId);
        visited.Add(parentId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (parentMap.TryGetValue(current, out var parents))
            {
                foreach (var parent in parents)
                {
                    if (string.Equals(parent, childId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (visited.Add(parent))
                    {
                        queue.Enqueue(parent);
                    }
                }
            }
        }

        return false;
    }

    private static (Issue moved, IReadOnlyList<Issue> siblings) Move(
        Issue child,
        string parentId,
        IReadOnlyList<Issue> allIssues,
        MoveDirection direction)
    {
        var siblings = GetSortedSiblings(parentId, allIssues);

        var childIndex = siblings.FindIndex(s =>
            string.Equals(s.Issue.Id, child.Id, StringComparison.OrdinalIgnoreCase));

        if (childIndex < 0)
        {
            throw new InvalidOperationException(
                $"Issue '{child.Id}' is not a child of '{parentId}'");
        }

        if (direction == MoveDirection.Up && childIndex == 0)
        {
            throw new InvalidOperationException(
                $"Issue '{child.Id}' is already at the top");
        }

        if (direction == MoveDirection.Down && childIndex == siblings.Count - 1)
        {
            throw new InvalidOperationException(
                $"Issue '{child.Id}' is already at the bottom");
        }

        // Track siblings modified by normalization
        var modifiedSiblings = new List<Issue>();

        // Determine the boundary ranks for the new position
        string? beforeRank;
        string? afterRank;

        if (direction == MoveDirection.Up)
        {
            afterRank = siblings[childIndex - 1].SortOrder;
            beforeRank = childIndex - 2 >= 0 ? siblings[childIndex - 2].SortOrder : null;
        }
        else
        {
            beforeRank = siblings[childIndex + 1].SortOrder;
            afterRank = childIndex + 2 < siblings.Count ? siblings[childIndex + 2].SortOrder : null;
        }

        // Check if normalization is needed
        if (NeedsNormalization(beforeRank, afterRank))
        {
            var (normalizedSiblings, normalizedIssues) = NormalizeSiblingRanks(parentId, siblings);
            siblings = normalizedSiblings;
            modifiedSiblings.AddRange(normalizedIssues);

            childIndex = siblings.FindIndex(s =>
                string.Equals(s.Issue.Id, child.Id, StringComparison.OrdinalIgnoreCase));

            if (direction == MoveDirection.Up)
            {
                afterRank = siblings[childIndex - 1].SortOrder;
                beforeRank = childIndex - 2 >= 0 ? siblings[childIndex - 2].SortOrder : null;
            }
            else
            {
                beforeRank = siblings[childIndex + 1].SortOrder;
                afterRank = childIndex + 2 < siblings.Count ? siblings[childIndex + 2].SortOrder : null;
            }
        }

        var newSortOrder = LexoRank.GetMiddleRank(beforeRank, afterRank);

        var newParentIssues = UpdateSortOrderForParent(child.ParentIssues, parentId, newSortOrder);
        var movedIssue = child with { ParentIssues = newParentIssues };

        return (movedIssue, modifiedSiblings);
    }

    internal static List<SiblingInfo> GetSortedSiblings(
        string parentId,
        IReadOnlyList<Issue> allIssues)
    {
        return allIssues
            .Where(i => i.ActiveParentIssues.Any(p =>
                string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase)))
            .Select(i => new SiblingInfo(
                i,
                i.ActiveParentIssues
                    .First(p => string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase))
                    .SortOrder))
            .OrderBy(x => x.SortOrder, StringComparer.Ordinal)
            .ToList();
    }

    internal static string ComputeSortOrder(
        string parentId,
        DependencyPosition position,
        IReadOnlyList<Issue> allIssues)
    {
        var siblings = GetSortedSiblings(parentId, allIssues);

        return position.Kind switch
        {
            DependencyPositionKind.First => siblings.Count == 0
                ? LexoRank.GetMiddleRank(null, null)
                : LexoRank.GetMiddleRank(null, siblings[0].SortOrder),

            DependencyPositionKind.Last => siblings.Count == 0
                ? LexoRank.GetMiddleRank(null, null)
                : LexoRank.GetMiddleRank(siblings[^1].SortOrder, null),

            DependencyPositionKind.After => ComputeAfterPosition(siblings, position.SiblingId!, parentId),

            DependencyPositionKind.Before => ComputeBeforePosition(siblings, position.SiblingId!, parentId),

            _ => LexoRank.GetMiddleRank(null, null)
        };
    }

    private static string ComputeAfterPosition(
        List<SiblingInfo> siblings,
        string siblingId,
        string parentId)
    {
        var siblingIndex = siblings.FindIndex(s =>
            string.Equals(s.Issue.Id, siblingId, StringComparison.OrdinalIgnoreCase));

        if (siblingIndex < 0)
        {
            throw new InvalidOperationException(
                $"Issue '{siblingId}' is not a child of '{parentId}'");
        }

        var afterRank = siblings[siblingIndex].SortOrder;
        var beforeRank = siblingIndex + 1 < siblings.Count
            ? siblings[siblingIndex + 1].SortOrder
            : null;

        return LexoRank.GetMiddleRank(afterRank, beforeRank);
    }

    private static string ComputeBeforePosition(
        List<SiblingInfo> siblings,
        string siblingId,
        string parentId)
    {
        var siblingIndex = siblings.FindIndex(s =>
            string.Equals(s.Issue.Id, siblingId, StringComparison.OrdinalIgnoreCase));

        if (siblingIndex < 0)
        {
            throw new InvalidOperationException(
                $"Issue '{siblingId}' is not a child of '{parentId}'");
        }

        var beforeRank = siblings[siblingIndex].SortOrder;
        var afterRank = siblingIndex > 0
            ? siblings[siblingIndex - 1].SortOrder
            : null;

        return LexoRank.GetMiddleRank(afterRank, beforeRank);
    }

    internal static bool NeedsNormalization(string? beforeRank, string? afterRank)
    {
        if (beforeRank is null || afterRank is null)
        {
            return false;
        }

        var middle = LexoRank.GetMiddleRank(beforeRank, afterRank);
        return middle.Length > Math.Max(beforeRank.Length, afterRank.Length);
    }

    internal static (List<SiblingInfo> normalized, List<Issue> modifiedIssues) NormalizeSiblingRanks(
        string parentId,
        List<SiblingInfo> siblings)
    {
        var ranks = LexoRank.GenerateInitialRanks(siblings.Count);
        var normalized = new List<SiblingInfo>(siblings.Count);
        var modifiedIssues = new List<Issue>(siblings.Count);

        for (var i = 0; i < siblings.Count; i++)
        {
            var sibling = siblings[i];
            var newParentIssues = UpdateSortOrderForParent(
                sibling.Issue.ParentIssues, parentId, ranks[i]);

            var updatedIssue = sibling.Issue with { ParentIssues = newParentIssues };
            normalized.Add(new SiblingInfo(updatedIssue, ranks[i]));
            modifiedIssues.Add(updatedIssue);
        }

        return (normalized, modifiedIssues);
    }

    internal static IReadOnlyList<ParentIssueRef> UpdateSortOrderForParent(
        IReadOnlyList<ParentIssueRef> parentIssues,
        string parentId,
        string newSortOrder)
    {
        return parentIssues.Select(p =>
            string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase)
                ? p with { SortOrder = newSortOrder }
                : p).ToList();
    }

    private enum MoveDirection { Up, Down }

    internal sealed record SiblingInfo(Issue Issue, string SortOrder);
}
