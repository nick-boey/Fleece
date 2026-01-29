namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Service for git operations on the .fleece directory.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Checks if git is available on the system.
    /// </summary>
    bool IsGitAvailable();

    /// <summary>
    /// Checks if the current directory is within a git repository.
    /// </summary>
    bool IsGitRepository();

    /// <summary>
    /// Checks if there are staged or unstaged changes in the .fleece directory.
    /// </summary>
    bool HasFleeceChanges();

    /// <summary>
    /// Stages all files in the .fleece directory.
    /// </summary>
    /// <returns>Result indicating success or failure with error message.</returns>
    GitOperationResult StageFleeceDirectory();

    /// <summary>
    /// Creates a commit with the staged changes.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    GitOperationResult Commit(string message);

    /// <summary>
    /// Pushes committed changes to the remote.
    /// </summary>
    /// <returns>Result indicating success or failure with error message.</returns>
    GitOperationResult Push();

    /// <summary>
    /// Convenience method: stages .fleece directory and commits with message.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    GitOperationResult CommitFleeceChanges(string message);

    /// <summary>
    /// Convenience method: stages, commits, and pushes .fleece directory changes.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    GitOperationResult CommitAndPushFleeceChanges(string message);
}

/// <summary>
/// Result of a git operation.
/// </summary>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="ErrorMessage">Error message if operation failed, null otherwise.</param>
public record GitOperationResult(bool Success, string? ErrorMessage = null)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static GitOperationResult Ok() => new(true);

    /// <summary>
    /// Creates a failed result with error message.
    /// </summary>
    public static GitOperationResult Fail(string error) => new(false, error);
}
