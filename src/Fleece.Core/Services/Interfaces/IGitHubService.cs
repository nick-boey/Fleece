using Fleece.Core.Models.GitHub;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Abstraction over GitHub API access. The OctoKit-backed implementation lives in the
/// <c>Fleece.GitHub</c> assembly so that <c>Fleece.Core</c> carries no OctoKit dependency
/// and the E2E suite can substitute a fake.
/// </summary>
public interface IGitHubService
{
    /// <summary>
    /// Resolves a GitHub token in the order <c>gh auth token</c>, then
    /// <c>GH_TOKEN</c>/<c>GITHUB_TOKEN</c>, then a config-stored PAT, and determines the
    /// target repository from the origin remote.
    /// </summary>
    Task<GitHubAuthResult> ResolveAuthAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the login of the currently authenticated user, or null when unauthenticated.</summary>
    Task<string?> GetCurrentLoginAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a GitHub issue and returns its number and URL.</summary>
    Task<GitHubIssueRef> CreateIssueAsync(string title, string body, CancellationToken cancellationToken = default);

    /// <summary>Fetches an existing GitHub issue by number.</summary>
    Task<GitHubIssue> GetIssueAsync(int number, CancellationToken cancellationToken = default);

    /// <summary>Adds a comment to a GitHub issue.</summary>
    Task AddCommentAsync(int number, string body, CancellationToken cancellationToken = default);

    /// <summary>Assigns a GitHub issue to the given login.</summary>
    Task AssignAsync(int number, string login, CancellationToken cancellationToken = default);
}
