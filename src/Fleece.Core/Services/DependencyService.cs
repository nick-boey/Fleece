using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

#pragma warning disable CS0618 // Type or member is obsolete - Internal service uses obsolete linkedPr param intentionally

namespace Fleece.Core.Services;

/// <summary>
/// Service for managing parent-child dependency relationships between issues.
/// Loads/resolves issues, delegates to pure functions in <see cref="Dependencies"/>, and persists results.
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
        var resolvedParent = await ResolveIssueAsync(parentId, "parent", ct);
        var resolvedChild = await ResolveIssueAsync(childId, "child", ct);

        // Check for circular dependency via validation service
        if (await _validationService.WouldCreateCycleAsync(resolvedParent.Id, resolvedChild.Id, ct))
        {
            throw new InvalidOperationException(
                $"Adding '{resolvedParent.Id}' as a parent of '{resolvedChild.Id}' would create a circular dependency");
        }

        var allIssues = await _issueService.GetAllAsync(ct);

        var updated = Dependencies.AddDependency(
            resolvedChild, resolvedParent.Id, allIssues, position, replaceExisting, makePrimary);

        return await _issueService.UpdateAsync(
            id: resolvedChild.Id,
            parentIssues: updated.ParentIssues,
            cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task<Issue> RemoveDependencyAsync(
        string parentId,
        string childId,
        CancellationToken ct = default)
    {
        var resolvedParent = await ResolveIssueAsync(parentId, "parent", ct);
        var resolvedChild = await ResolveIssueAsync(childId, "child", ct);

        var updated = Dependencies.RemoveDependency(resolvedChild, resolvedParent.Id);

        return await _issueService.UpdateAsync(
            id: resolvedChild.Id,
            parentIssues: updated.ParentIssues,
            cancellationToken: ct);
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
        var allIssues = await _issueService.GetAllAsync(ct);

        // Check if child is actually under this parent
        var isChild = resolvedChild.ParentIssues.Any(p =>
            string.Equals(p.ParentIssue, resolvedParent.Id, StringComparison.OrdinalIgnoreCase));

        if (!isChild)
        {
            return new MoveResult
            {
                Outcome = MoveOutcome.Invalid,
                Reason = MoveInvalidReason.NotAChildOfParent,
                Message = $"Issue '{resolvedChild.Id}' is not a child of '{resolvedParent.Id}'"
            };
        }

        // Check boundary conditions
        var siblings = Dependencies.GetSortedSiblings(resolvedParent.Id, allIssues);
        var childIndex = siblings.FindIndex(s =>
            string.Equals(s.Issue.Id, resolvedChild.Id, StringComparison.OrdinalIgnoreCase));

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

        // Delegate to pure function
        var (movedIssue, modifiedSiblings) = direction == MoveDirection.Up
            ? Dependencies.MoveUp(resolvedChild, resolvedParent.Id, allIssues)
            : Dependencies.MoveDown(resolvedChild, resolvedParent.Id, allIssues);

        // Persist normalized siblings first
        foreach (var sibling in modifiedSiblings)
        {
            await _issueService.UpdateAsync(
                id: sibling.Id,
                parentIssues: sibling.ParentIssues,
                cancellationToken: ct);
        }

        // Persist the moved issue
        var updatedIssue = await _issueService.UpdateAsync(
            id: movedIssue.Id,
            parentIssues: movedIssue.ParentIssues,
            cancellationToken: ct);

        return new MoveResult
        {
            Outcome = direction == MoveDirection.Up ? MoveOutcome.MovedUp : MoveOutcome.MovedDown,
            UpdatedIssue = updatedIssue
        };
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

    private enum MoveDirection { Up, Down }
}
