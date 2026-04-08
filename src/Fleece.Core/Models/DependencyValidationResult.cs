namespace Fleece.Core.Models;

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
