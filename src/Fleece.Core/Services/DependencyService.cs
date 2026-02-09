using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Utilities;

namespace Fleece.Core.Services;

/// <summary>
/// Service for managing parent-child dependency relationships between issues.
/// </summary>
public class DependencyService : IDependencyService
{
    private readonly IIssueService _issueService;
    private readonly IValidationService _validationService;

    public DependencyService(IIssueService issueService, IValidationService validationService)
    {
        _issueService = issueService;
        _validationService = validationService;
    }

    /// <inheritdoc />
    public async Task<Issue> AddDependencyAsync(
        string parentId,
        string childId,
        DependencyPosition? position = null,
        CancellationToken ct = default)
    {
        position ??= new DependencyPosition();

        // Resolve parent and child IDs
        var resolvedParent = await ResolveIssueAsync(parentId, "parent", ct);
        var resolvedChild = await ResolveIssueAsync(childId, "child", ct);

        // Check relationship doesn't already exist
        if (resolvedChild.ParentIssues.Any(p =>
            string.Equals(p.ParentIssue, resolvedParent.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Issue '{resolvedChild.Id}' is already a child of '{resolvedParent.Id}'");
        }

        // Check for circular dependency
        if (await _validationService.WouldCreateCycleAsync(resolvedParent.Id, resolvedChild.Id, ct))
        {
            throw new InvalidOperationException(
                $"Adding '{resolvedParent.Id}' as a parent of '{resolvedChild.Id}' would create a circular dependency");
        }

        // Compute sort order based on position
        var sortOrder = await ComputeSortOrderAsync(resolvedParent.Id, position, ct);

        // Build new parent issues list
        var newParentIssues = resolvedChild.ParentIssues.ToList();
        newParentIssues.Add(new ParentIssueRef
        {
            ParentIssue = resolvedParent.Id,
            SortOrder = sortOrder
        });

        return await _issueService.UpdateAsync(
            id: resolvedChild.Id,
            parentIssues: newParentIssues,
            cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task<Issue> RemoveDependencyAsync(
        string parentId,
        string childId,
        CancellationToken ct = default)
    {
        // Resolve parent and child IDs
        var resolvedParent = await ResolveIssueAsync(parentId, "parent", ct);
        var resolvedChild = await ResolveIssueAsync(childId, "child", ct);

        // Check relationship exists
        var existingRef = resolvedChild.ParentIssues.FirstOrDefault(p =>
            string.Equals(p.ParentIssue, resolvedParent.Id, StringComparison.OrdinalIgnoreCase));

        if (existingRef is null)
        {
            throw new InvalidOperationException(
                $"Issue '{resolvedChild.Id}' is not a child of '{resolvedParent.Id}'");
        }

        // Remove the parent from the child's parent issues list
        var newParentIssues = resolvedChild.ParentIssues
            .Where(p => !string.Equals(p.ParentIssue, resolvedParent.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return await _issueService.UpdateAsync(
            id: resolvedChild.Id,
            parentIssues: newParentIssues,
            cancellationToken: ct);
    }

    private async Task<Issue> ResolveIssueAsync(string partialId, string role, CancellationToken ct)
    {
        var matches = await _issueService.ResolveByPartialIdAsync(partialId, ct);

        return matches.Count switch
        {
            0 => throw new KeyNotFoundException($"No {role} issue found matching '{partialId}'"),
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Multiple issues match '{partialId}': {string.Join(", ", matches.Select(m => m.Id))}")
        };
    }

    private async Task<string> ComputeSortOrderAsync(
        string parentId,
        DependencyPosition position,
        CancellationToken ct)
    {
        // Find all existing siblings (issues that have this parent)
        var allIssues = await _issueService.GetAllAsync(ct);
        var siblings = allIssues
            .Where(i => i.ParentIssues.Any(p =>
                string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase)))
            .Select(i => new SiblingInfo(
                i,
                i.ParentIssues
                    .First(p => string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase))
                    .SortOrder))
            .OrderBy(x => x.SortOrder, StringComparer.Ordinal)
            .ToList();

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

    private sealed record SiblingInfo(Issue Issue, string SortOrder);
}
