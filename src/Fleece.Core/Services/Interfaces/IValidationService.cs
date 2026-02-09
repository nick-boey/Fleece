namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Service for validating issue data integrity.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates that there are no cyclic dependencies in PreviousIssues relationships.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Validation result containing any detected cycles.</returns>
    Task<DependencyValidationResult> ValidateDependencyCyclesAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks whether adding a parent-child edge from <paramref name="parentId"/> to
    /// <paramref name="childId"/> would create a cycle in the dependency graph.
    /// </summary>
    /// <param name="parentId">The full ID of the proposed parent issue.</param>
    /// <param name="childId">The full ID of the proposed child issue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the proposed edge would create a cycle; otherwise false.</returns>
    Task<bool> WouldCreateCycleAsync(string parentId, string childId, CancellationToken ct = default);
}

/// <summary>
/// Result of a dependency validation check.
/// </summary>
/// <param name="IsValid">True if no cycles were detected.</param>
/// <param name="Cycles">List of detected dependency cycles.</param>
public record DependencyValidationResult(bool IsValid, IReadOnlyList<DependencyCycle> Cycles);

/// <summary>
/// Represents a cycle in issue dependencies.
/// </summary>
/// <param name="IssueIds">The issue IDs forming the cycle, with the first ID repeated at the end.</param>
public record DependencyCycle(IReadOnlyList<string> IssueIds);
