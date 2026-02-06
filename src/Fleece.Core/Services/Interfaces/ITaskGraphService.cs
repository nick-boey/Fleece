using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Service for building a task graph layout from issues.
/// The graph is organized bottom-up, with actionable tasks at the left (lane 0)
/// and root/parent tasks at the right (higher lanes).
/// </summary>
public interface ITaskGraphService
{
    /// <summary>
    /// Builds a task graph layout from all non-terminal issues.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A positioned task graph ready for rendering.</returns>
    Task<TaskGraph> BuildGraphAsync(CancellationToken cancellationToken = default);
}
