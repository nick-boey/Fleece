using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Unified service for building and querying issue graphs with computed relationships.
/// Consolidates functionality from NextService (actionable issues) and TaskGraphService (layout).
/// </summary>
public interface IIssueGraphService
{
    // --- Core Graph Building ---

    /// <summary>
    /// Builds a complete issue graph from all issues.
    /// The graph includes parent-child and next/previous relationships.
    /// Next/Previous are only computed for siblings under Series execution mode parents.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A complete issue graph.</returns>
    Task<IssueGraph> BuildGraphAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a filtered subgraph based on the query parameters.
    ///
    /// IMPORTANT: Next/Previous relationships are computed from the FULL graph,
    /// then the subgraph is filtered. This means relationships reflect the actual
    /// execution order in the full hierarchy.
    /// </summary>
    /// <param name="query">Query parameters for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A filtered subgraph.</returns>
    Task<IssueGraph> QueryGraphAsync(
        GraphQuery query,
        CancellationToken cancellationToken = default);

    // --- Actionable Issues (replaces INextService) ---

    /// <summary>
    /// Gets all issues that are currently actionable (can be worked on next).
    /// An issue is actionable if:
    /// - It has Open or Review status
    /// - All its PreviousIssueIds are done (Complete, Archived, or Closed)
    /// - It has no incomplete children
    /// - Its parent's ExecutionMode allows it (for Series, it must be the first incomplete child)
    ///
    /// Results are sorted by: Review status > description presence > priority > title.
    /// </summary>
    /// <param name="parentId">Optional parent ID to filter results to descendants of that parent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of actionable issues.</returns>
    Task<IReadOnlyList<Issue>> GetNextIssuesAsync(
        string? parentId = null,
        CancellationToken cancellationToken = default);

    // --- Task Graph Layout (replaces ITaskGraphService) ---

    /// <summary>
    /// Builds a positioned task graph layout for rendering.
    /// The graph is organized bottom-up, with actionable tasks at the left (lane 0)
    /// and root/parent tasks at the right (higher lanes).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A positioned task graph ready for rendering.</returns>
    Task<TaskGraph> BuildTaskGraphLayoutAsync(CancellationToken cancellationToken = default);
}
