using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

public interface IStorageService
{
    Task<IReadOnlyList<Issue>> LoadIssuesAsync(CancellationToken cancellationToken = default);
    Task SaveIssuesAsync(IReadOnlyList<Issue> issues, CancellationToken cancellationToken = default);
    Task AppendIssueAsync(Issue issue, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChangeRecord>> LoadChangesAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(IReadOnlyList<ChangeRecord> changes, CancellationToken cancellationToken = default);
    Task AppendChangeAsync(ChangeRecord change, CancellationToken cancellationToken = default);
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

    /// <summary>
    /// Checks if there are multiple unmerged issue or changes files.
    /// </summary>
    /// <returns>A tuple indicating if multiple files exist and an error message to display.</returns>
    Task<(bool HasMultiple, string Message)> HasMultipleUnmergedFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all issues with diagnostic information about parsing.
    /// </summary>
    /// <returns>Result containing issues and parsing diagnostics per file.</returns>
    Task<LoadIssuesResult> LoadIssuesWithDiagnosticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all tombstones from all tombstone files.
    /// </summary>
    Task<IReadOnlyList<Tombstone>> LoadTombstonesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves tombstones, replacing all existing tombstone files with a single consolidated file.
    /// </summary>
    Task SaveTombstonesAsync(IReadOnlyList<Tombstone> tombstones, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends tombstones to the existing tombstone file.
    /// </summary>
    Task AppendTombstonesAsync(IReadOnlyList<Tombstone> tombstones, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tombstone files in the fleece directory.
    /// </summary>
    Task<IReadOnlyList<string>> GetAllTombstoneFilesAsync(CancellationToken cancellationToken = default);
}
