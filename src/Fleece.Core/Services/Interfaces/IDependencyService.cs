using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Service for managing parent-child dependency relationships between issues.
/// </summary>
public interface IDependencyService
{
    /// <summary>
    /// Adds a parent-child dependency between two issues.
    /// Validates that both IDs exist, the relationship doesn't already exist,
    /// and no circular dependency would be created.
    /// </summary>
    /// <param name="parentId">The parent issue ID (may be partial, 3+ chars).</param>
    /// <param name="childId">The child issue ID (may be partial, 3+ chars).</param>
    /// <param name="position">Sort order positioning for the child within the parent's children.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated child issue.</returns>
    Task<Issue> AddDependencyAsync(
        string parentId,
        string childId,
        DependencyPosition? position = null,
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
}
