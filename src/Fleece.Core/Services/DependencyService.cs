using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Utilities;

#pragma warning disable CS0618 // Type or member is obsolete - Internal service uses obsolete linkedPr param intentionally

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
        bool replaceExisting = false,
        bool makePrimary = false,
        CancellationToken ct = default)
    {
        position ??= new DependencyPosition();

        // Resolve parent and child IDs
        var resolvedParent = await ResolveIssueAsync(parentId, "parent", ct);
        var resolvedChild = await ResolveIssueAsync(childId, "child", ct);

        // Check relationship doesn't already exist (only when not replacing)
        if (!replaceExisting && resolvedChild.ParentIssues.Any(p =>
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
        List<ParentIssueRef> newParentIssues;

        if (replaceExisting)
        {
            // Replace all existing parents with just the new one
            newParentIssues = new List<ParentIssueRef>();
        }
        else
        {
            // Preserve existing parents and add the new one (default)
            newParentIssues = resolvedChild.ParentIssues.ToList();
        }

        var newParentRef = new ParentIssueRef
        {
            ParentIssue = resolvedParent.Id,
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

    /// <inheritdoc />
    public async Task<MoveResult> MoveUpAsync(
        string parentId,
        string childId,
        CancellationToken ct = default)
    {
        return await MoveAsync(parentId, childId, MoveDirection.Up, ct);
    }

    /// <inheritdoc />
    public async Task<MoveResult> MoveDownAsync(
        string parentId,
        string childId,
        CancellationToken ct = default)
    {
        return await MoveAsync(parentId, childId, MoveDirection.Down, ct);
    }

    private async Task<MoveResult> MoveAsync(
        string parentId,
        string childId,
        MoveDirection direction,
        CancellationToken ct)
    {
        var resolvedParent = await ResolveIssueAsync(parentId, "parent", ct);
        var resolvedChild = await ResolveIssueAsync(childId, "child", ct);

        var siblings = await GetSortedSiblingsAsync(resolvedParent.Id, ct);

        var childIndex = siblings.FindIndex(s =>
            string.Equals(s.Issue.Id, resolvedChild.Id, StringComparison.OrdinalIgnoreCase));

        if (childIndex < 0)
        {
            return new MoveResult
            {
                Outcome = MoveOutcome.Invalid,
                Reason = MoveInvalidReason.NotAChildOfParent,
                Message = $"Issue '{resolvedChild.Id}' is not a child of '{resolvedParent.Id}'"
            };
        }

        if (direction == MoveDirection.Up && childIndex == 0)
        {
            return new MoveResult
            {
                Outcome = MoveOutcome.Invalid,
                Reason = MoveInvalidReason.AlreadyAtTop,
                Message = $"Issue '{resolvedChild.Id}' is already at the top"
            };
        }

        if (direction == MoveDirection.Down && childIndex == siblings.Count - 1)
        {
            return new MoveResult
            {
                Outcome = MoveOutcome.Invalid,
                Reason = MoveInvalidReason.AlreadyAtBottom,
                Message = $"Issue '{resolvedChild.Id}' is already at the bottom"
            };
        }

        // Determine the boundary ranks for the new position
        string? beforeRank;
        string? afterRank;

        if (direction == MoveDirection.Up)
        {
            // Moving up: place between sibling[index-2] and sibling[index-1]
            afterRank = siblings[childIndex - 1].SortOrder;
            beforeRank = childIndex - 2 >= 0 ? siblings[childIndex - 2].SortOrder : null;
        }
        else
        {
            // Moving down: place between sibling[index+1] and sibling[index+2]
            beforeRank = siblings[childIndex + 1].SortOrder;
            afterRank = childIndex + 2 < siblings.Count ? siblings[childIndex + 2].SortOrder : null;
        }

        // Check if normalization is needed before computing the rank
        if (NeedsNormalization(beforeRank, afterRank))
        {
            siblings = await NormalizeSiblingRanksAsync(resolvedParent.Id, siblings, ct);

            // Recompute the child index (same position, new ranks)
            childIndex = siblings.FindIndex(s =>
                string.Equals(s.Issue.Id, resolvedChild.Id, StringComparison.OrdinalIgnoreCase));

            // Recompute boundary ranks with normalized values
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

        // Update the moved issue's ParentIssueRef for this parent
        var newParentIssues = UpdateSortOrderForParent(
            resolvedChild.ParentIssues, resolvedParent.Id, newSortOrder);

        var updatedIssue = await _issueService.UpdateAsync(
            id: resolvedChild.Id,
            parentIssues: newParentIssues,
            cancellationToken: ct);

        return new MoveResult
        {
            Outcome = direction == MoveDirection.Up ? MoveOutcome.MovedUp : MoveOutcome.MovedDown,
            UpdatedIssue = updatedIssue
        };
    }

    private async Task<List<SiblingInfo>> GetSortedSiblingsAsync(
        string parentId, CancellationToken ct)
    {
        var allIssues = await _issueService.GetAllAsync(ct);
        return allIssues
            .Where(i => i.ParentIssues.Any(p =>
                string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase)))
            .Select(i => new SiblingInfo(
                i,
                i.ParentIssues
                    .First(p => string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase))
                    .SortOrder))
            .OrderBy(x => x.SortOrder, StringComparer.Ordinal)
            .ToList();
    }

    private static bool NeedsNormalization(string? beforeRank, string? afterRank)
    {
        // Normalization is only needed when inserting between two ranks that are adjacent
        // (would require precision extension). At boundaries (null before or after),
        // LexoRank can always produce a valid rank by extending precision, which is acceptable.
        if (beforeRank is null || afterRank is null)
        {
            return false;
        }

        // Check if the result would require precision extension
        var middle = LexoRank.GetMiddleRank(beforeRank, afterRank);
        return middle.Length > Math.Max(beforeRank.Length, afterRank.Length);
    }

    private async Task<List<SiblingInfo>> NormalizeSiblingRanksAsync(
        string parentId,
        List<SiblingInfo> siblings,
        CancellationToken ct)
    {
        var ranks = LexoRank.GenerateInitialRanks(siblings.Count);
        var normalized = new List<SiblingInfo>(siblings.Count);

        for (var i = 0; i < siblings.Count; i++)
        {
            var sibling = siblings[i];
            var newParentIssues = UpdateSortOrderForParent(
                sibling.Issue.ParentIssues, parentId, ranks[i]);

            await _issueService.UpdateAsync(
                id: sibling.Issue.Id,
                parentIssues: newParentIssues,
                cancellationToken: ct);

            normalized.Add(new SiblingInfo(sibling.Issue, ranks[i]));
        }

        return normalized;
    }

    private static IReadOnlyList<ParentIssueRef> UpdateSortOrderForParent(
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

    private sealed record SiblingInfo(Issue Issue, string SortOrder);
}
