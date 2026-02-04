using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Service for finding issues that can be worked on next based on dependencies and execution mode.
/// </summary>
public interface INextService
{
    /// <summary>
    /// Gets all issues that are currently actionable (can be worked on next).
    /// An issue is actionable if:
    /// - It has Open status
    /// - All its PreviousIssues are done (Complete, Archived, or Closed)
    /// - Its parent's ExecutionMode allows it (for Series, it must be the first incomplete child)
    /// </summary>
    /// <param name="parentId">Optional parent ID to filter results to descendants of that parent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of actionable issues.</returns>
    Task<IReadOnlyList<Issue>> GetNextIssuesAsync(
        string? parentId = null,
        CancellationToken cancellationToken = default);
}
