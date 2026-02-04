using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Service for finding issues that can be worked on next based on dependencies and execution mode.
/// </summary>
public sealed class NextService(IIssueService issueService) : INextService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Issue>> GetNextIssuesAsync(
        string? parentId = null,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await issueService.GetAllAsync(cancellationToken);
        var issueList = allIssues.ToList();

        if (issueList.Count == 0)
        {
            return [];
        }

        // Build lookup for quick access
        var issueLookup = issueList.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Find actionable issues
        var actionable = issueList
            .Where(issue => IsActionable(issue, issueLookup))
            .ToList();

        // Apply parent filter if specified
        if (!string.IsNullOrWhiteSpace(parentId))
        {
            var descendants = GetDescendantIds(parentId, issueList);
            actionable = actionable.Where(i => descendants.Contains(i.Id)).ToList();
        }

        return actionable;
    }

    /// <summary>
    /// Determines if an issue is actionable (can be worked on next).
    /// </summary>
    private static bool IsActionable(Issue issue, Dictionary<string, Issue> issueLookup)
    {
        // Must be Open status to be actionable
        if (issue.Status != IssueStatus.Open)
        {
            return false;
        }

        // Check if all PreviousIssues are done
        if (!ArePreviousIssuesDone(issue, issueLookup))
        {
            return false;
        }

        // Check if parent's ExecutionMode allows this issue to be worked on
        if (!IsAllowedByParentExecutionMode(issue, issueLookup))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if all PreviousIssues for this issue are done.
    /// </summary>
    private static bool ArePreviousIssuesDone(Issue issue, Dictionary<string, Issue> issueLookup)
    {
        if (issue.PreviousIssues.Count == 0)
        {
            return true;
        }

        foreach (var prevId in issue.PreviousIssues)
        {
            // If previous issue doesn't exist, treat as done
            if (!issueLookup.TryGetValue(prevId, out var prevIssue))
            {
                continue;
            }

            // If previous issue is not done, this issue is blocked
            if (!prevIssue.Status.IsDone())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if this issue is allowed to be worked on based on parent's ExecutionMode.
    /// For Series mode, only the first incomplete child is allowed.
    /// For Parallel mode, all children are allowed.
    /// </summary>
    private static bool IsAllowedByParentExecutionMode(Issue issue, Dictionary<string, Issue> issueLookup)
    {
        if (issue.ParentIssues.Count == 0)
        {
            return true;
        }

        // Check each parent - all parents must allow this issue
        foreach (var parentId in issue.ParentIssues)
        {
            // If parent doesn't exist, treat as parallel (allow)
            if (!issueLookup.TryGetValue(parentId, out var parent))
            {
                continue;
            }

            // For parallel mode, always allowed
            if (parent.ExecutionMode == ExecutionMode.Parallel)
            {
                continue;
            }

            // For series mode, this issue must be the first incomplete child
            if (!IsFirstIncompleteChild(issue, parent, issueLookup))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines if the given issue is the first incomplete child of the parent (in sort order).
    /// Sort order: Priority (ascending, null last) then Title (alphabetic).
    /// </summary>
    private static bool IsFirstIncompleteChild(Issue issue, Issue parent, Dictionary<string, Issue> issueLookup)
    {
        // Get all children of this parent
        var children = issueLookup.Values
            .Where(i => i.ParentIssues.Contains(parent.Id, StringComparer.OrdinalIgnoreCase))
            .OrderBy(i => i.Priority ?? int.MaxValue)
            .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Find the first incomplete child
        var firstIncomplete = children.FirstOrDefault(c => !c.Status.IsDone());

        // This issue is allowed if it's the first incomplete child
        return firstIncomplete != null &&
               string.Equals(firstIncomplete.Id, issue.Id, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets all descendant IDs (children, grandchildren, etc.) of the given parent.
    /// </summary>
    private static HashSet<string> GetDescendantIds(string parentId, List<Issue> allIssues)
    {
        var descendants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toProcess = new Queue<string>();
        toProcess.Enqueue(parentId);

        while (toProcess.Count > 0)
        {
            var currentId = toProcess.Dequeue();
            var children = allIssues
                .Where(i => i.ParentIssues.Contains(currentId, StringComparer.OrdinalIgnoreCase))
                .Select(i => i.Id)
                .ToList();

            foreach (var childId in children)
            {
                if (descendants.Add(childId))
                {
                    toProcess.Enqueue(childId);
                }
            }
        }

        return descendants;
    }
}
