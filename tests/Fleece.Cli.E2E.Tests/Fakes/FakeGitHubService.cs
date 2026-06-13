using Fleece.Core.Models.GitHub;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Cli.E2E.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IGitHubService"/> for hermetic E2E tests. Records calls and returns
/// scripted values so the GitHub command surfaces can be exercised without network access.
/// </summary>
public sealed class FakeGitHubService : IGitHubService
{
    public GitHubAuthResult AuthResult { get; set; } = new()
    {
        Authenticated = true,
        Login = "test-user",
        TokenSource = "fake",
        Repository = "owner/name"
    };

    public int NextIssueNumber { get; set; } = 1;

    public List<GitHubIssue> Issues { get; } = [];
    public List<(int Number, string Title, string Body)> CreatedIssues { get; } = [];
    public List<(int Number, string Body)> Comments { get; } = [];
    public List<(int Number, string Login)> Assignments { get; } = [];

    public Task<GitHubAuthResult> ResolveAuthAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(AuthResult);

    public Task<string?> GetCurrentLoginAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(AuthResult.Login);

    public Task<GitHubIssueRef> CreateIssueAsync(string title, string body, CancellationToken cancellationToken = default)
    {
        var number = NextIssueNumber++;
        CreatedIssues.Add((number, title, body));
        return Task.FromResult(new GitHubIssueRef
        {
            Number = number,
            Url = $"https://github.com/{AuthResult.Repository}/issues/{number}"
        });
    }

    public Task<GitHubIssue> GetIssueAsync(int number, CancellationToken cancellationToken = default)
    {
        var issue = Issues.FirstOrDefault(i => i.Number == number)
            ?? new GitHubIssue { Number = number, Title = $"Issue {number}", Body = null };
        return Task.FromResult(issue);
    }

    public Task AddCommentAsync(int number, string body, CancellationToken cancellationToken = default)
    {
        Comments.Add((number, body));
        return Task.CompletedTask;
    }

    public Task AssignAsync(int number, string login, CancellationToken cancellationToken = default)
    {
        Assignments.Add((number, login));
        return Task.CompletedTask;
    }
}
