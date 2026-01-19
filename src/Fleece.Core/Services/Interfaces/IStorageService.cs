using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

public interface IStorageService
{
    Task<IReadOnlyList<Issue>> LoadIssuesAsync(CancellationToken cancellationToken = default);
    Task SaveIssuesAsync(IReadOnlyList<Issue> issues, CancellationToken cancellationToken = default);
    Task AppendIssueAsync(Issue issue, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConflictRecord>> LoadConflictsAsync(CancellationToken cancellationToken = default);
    Task SaveConflictsAsync(IReadOnlyList<ConflictRecord> conflicts, CancellationToken cancellationToken = default);
    Task AppendConflictAsync(ConflictRecord conflict, CancellationToken cancellationToken = default);
    Task EnsureDirectoryExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all issue files in the fleece directory (supports hashed filenames).
    /// </summary>
    Task<IReadOnlyList<string>> GetAllIssueFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads issues from a specific file.
    /// </summary>
    Task<IReadOnlyList<Issue>> LoadIssuesFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an issue file.
    /// </summary>
    Task DeleteIssueFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves issues and returns the path to the created file (with hashed filename).
    /// </summary>
    Task<string> SaveIssuesWithHashAsync(IReadOnlyList<Issue> issues, CancellationToken cancellationToken = default);
}
