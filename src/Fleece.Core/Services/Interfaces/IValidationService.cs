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
