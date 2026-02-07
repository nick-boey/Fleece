using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// In-memory cached issue service that provides fast reads from a ConcurrentDictionary cache,
/// keeps the cache synchronised with JSONL files on disk, and watches for external changes.
/// </summary>
public interface IFleeceInMemoryService : IDisposable
{
    #region Read Operations

    /// <summary>
    /// Gets a single issue by its ID from the in-memory cache.
    /// </summary>
    Task<Issue?> GetIssueAsync(string issueId, CancellationToken ct = default);

    /// <summary>
    /// Lists issues from the cache, optionally filtered by status, type, and/or priority.
    /// Excludes terminal statuses by default when no filters are specified.
    /// </summary>
    Task<IReadOnlyList<Issue>> ListIssuesAsync(
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        CancellationToken ct = default);

    /// <summary>
    /// Searches issues by matching the query against title, description, and tags.
    /// </summary>
    Task<IReadOnlyList<Issue>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Filters issues with multiple criteria. Excludes terminal statuses by default
    /// unless <paramref name="includeTerminal"/> is true or a specific status is requested.
    /// </summary>
    Task<IReadOnlyList<Issue>> FilterAsync(
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false,
        CancellationToken ct = default);

    #endregion

    #region Write Operations

    /// <summary>
    /// Creates a new issue, updating the cache immediately and queuing persistence to disk.
    /// </summary>
    Task<Issue> CreateIssueAsync(
        string title,
        IssueType type,
        string? description = null,
        int? priority = null,
        ExecutionMode? executionMode = null,
        IssueStatus? status = null,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing issue, updating the cache immediately and queuing persistence to disk.
    /// Returns null if the issue is not found.
    /// </summary>
    Task<Issue?> UpdateIssueAsync(
        string issueId,
        string? title = null,
        IssueStatus? status = null,
        IssueType? type = null,
        string? description = null,
        int? priority = null,
        ExecutionMode? executionMode = null,
        string? workingBranchId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes an issue (soft delete), updating the cache immediately and queuing persistence to disk.
    /// </summary>
    Task<bool> DeleteIssueAsync(string issueId, CancellationToken ct = default);

    #endregion

    #region Cache Management

    /// <summary>
    /// Forces a full reload of the cache from disk.
    /// </summary>
    Task ReloadAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a value indicating whether the cache has been loaded from disk at least once.
    /// </summary>
    bool IsLoaded { get; }

    #endregion
}
