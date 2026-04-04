using Fleece.Core.FunctionalCore;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Service for validating issue data integrity.
/// </summary>
public class ValidationService : IValidationService
{
    private readonly IIssueService _issueService;

    public ValidationService(IIssueService issueService)
    {
        _issueService = issueService;
    }

    /// <inheritdoc />
    public async Task<DependencyValidationResult> ValidateDependencyCyclesAsync(CancellationToken ct = default)
    {
        var issues = await _issueService.GetAllAsync(ct);
        return Validation.ValidateDependencyCycles(issues);
    }

    /// <inheritdoc />
    public async Task<bool> WouldCreateCycleAsync(string parentId, string childId, CancellationToken ct = default)
    {
        var issues = await _issueService.GetAllAsync(ct);
        return Validation.WouldCreateCycle(issues, parentId, childId);
    }
}
