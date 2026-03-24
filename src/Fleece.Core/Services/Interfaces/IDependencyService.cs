using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Service for managing parent-child dependency relationships between issues.
/// </summary>
public interface IDependencyService
{
    /// <summary>
    /// Adds a parent-child dependency between two issues.
    /// Validates that both IDs exist, the relationship doesn't already exist (unless replacing),
    /// and no circular dependency would be created.
    /// </summary>
    /// <param name="parentId">The parent issue ID (may be partial, 3+ chars).</param>
    /// <param name="childId">The child issue ID (may be partial, 3+ chars).</param>
    /// <param name="position">Sort order positioning for the child within the parent's children.</param>
    /// <param name="replaceExisting">If true, replace all existing parents instead of adding to them.</param>
    /// <param name="makePrimary">If true, make the new parent the primary (first in parent list).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated child issue.</returns>
    Task<Issue> AddDependencyAsync(
        string parentId,
        string childId,
        DependencyPosition? position = null,
        bool replaceExisting = false,
        bool makePrimary = false,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a parent-child dependency between two issues.
    /// Validates that both IDs exist and the relationship exists.
    /// </summary>
    /// <param name="parentId">The parent issue ID (may be partial, 3+ chars).</param>
    /// <param name="childId">The child issue ID (may be partial, 3+ chars).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated child issue.</returns>
    Task<Issue> RemoveDependencyAsync(
        string parentId,
        string childId,
        CancellationToken ct = default);

    /// <summary>
    /// Moves an issue up (before its previous sibling) within a parent's children.
    /// Only updates the moved issue's sort order. If there is insufficient rank space,
    /// all sibling ranks are normalized first.
    /// </summary>
    /// <param name="parentId">The parent issue ID (may be partial, 3+ chars).</param>
    /// <param name="childId">The child issue ID to move (may be partial, 3+ chars).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating whether the move succeeded or was invalid.</returns>
    Task<MoveResult> MoveUpAsync(
        string parentId,
        string childId,
        CancellationToken ct = default);

    /// <summary>
    /// Moves an issue down (after its next sibling) within a parent's children.
    /// Only updates the moved issue's sort order. If there is insufficient rank space,
    /// all sibling ranks are normalized first.
    /// </summary>
    /// <param name="parentId">The parent issue ID (may be partial, 3+ chars).</param>
    /// <param name="childId">The child issue ID to move (may be partial, 3+ chars).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating whether the move succeeded or was invalid.</returns>
    Task<MoveResult> MoveDownAsync(
        string parentId,
        string childId,
        CancellationToken ct = default);
}
