using Fleece.Core.Models;
using Fleece.Core.Models.GitHub;

namespace Fleece.Cli.E2E.Tests.Scenarios;

/// <summary>
/// Exercises the GitHub command surfaces (auth/promote/absorb) against the hermetic
/// <see cref="Fakes.FakeGitHubService"/> so no network access is required.
/// </summary>
[TestFixture]
[Category("auth")]
[Category("promote")]
[Category("absorb")]
public class GitHubScenarios : CliScenarioTestBase
{
    private async Task<string> CreateIssueAsync(string title, string description = "body")
    {
        await RunAsync("create", "-t", title, "-y", "task", "-d", description);
        return LoadIssues().Single(i => i.Title == title).Id;
    }

    [Test]
    public async Task Auth_authenticated_prints_login_and_exits_zero()
    {
        GitHub.AuthResult = new GitHubAuthResult
        {
            Authenticated = true,
            Login = "octocat",
            TokenSource = "gh auth token",
            Repository = "owner/name",
        };

        var exit = await RunWithGitHubAsync("auth");

        exit.Should().Be(0);
        Console.Output.Should().Contain("octocat");
    }

    [Test]
    public async Task Auth_unauthenticated_exits_non_zero()
    {
        GitHub.AuthResult = new GitHubAuthResult { Authenticated = false };

        var exit = await RunWithGitHubAsync("auth");

        exit.Should().Be(1);
    }

    [Test]
    public async Task Promote_creates_one_github_issue_and_marks_issues_promoted()
    {
        var a = await CreateIssueAsync("Bundle root");
        var b = await CreateIssueAsync("Bundle child");

        var exit = await RunWithGitHubAsync("promote", a, b);

        exit.Should().Be(0);
        GitHub.CreatedIssues.Should().HaveCount(1);
        GitHub.CreatedIssues[0].Title.Should().Be("Bundle root");
        GitHub.CreatedIssues[0].Body.Should().Contain(a).And.Contain(b);

        var issues = LoadIssues();
        // Sanity: marks every bundled issue promoted.
        foreach (var id in new[] { a, b })
        {
            var issue = issues.Single(i => i.Id == id);
            issue.Status.Should().Be(IssueStatus.Promoted);
            KeyedTag.HasKey(issue.Tags, "promoted").Should().BeTrue();
        }
    }

    [Test]
    public async Task Promote_body_includes_each_issue_description_and_title()
    {
        var a = await CreateIssueAsync("Implement widget", "The widget must support resizing and theming.");
        var b = await CreateIssueAsync("Document widget", "Add a usage guide to the README.");

        var exit = await RunWithGitHubAsync("promote", a, b);

        exit.Should().Be(0);
        var body = GitHub.CreatedIssues.Single().Body;
        body.Should().Contain("Implement widget").And.Contain("The widget must support resizing and theming.");
        body.Should().Contain("Document widget").And.Contain("Add a usage guide to the README.");
        body.Should().Contain(a).And.Contain(b);
    }

    [Test]
    public async Task Promote_body_falls_back_when_description_is_missing()
    {
        await RunAsync("create", "-t", "No description here", "-y", "task");
        var id = LoadIssues().Single(i => i.Title == "No description here").Id;

        var exit = await RunWithGitHubAsync("promote", id);

        exit.Should().Be(0);
        var body = GitHub.CreatedIssues.Single().Body;
        body.Should().Contain("No description here");
        body.Should().Contain("No description provided");
    }

    [Test]
    public async Task Promote_is_idempotent_and_skips_already_promoted_issues()
    {
        var a = await CreateIssueAsync("Already");

        (await RunWithGitHubAsync("promote", a)).Should().Be(0);
        GitHub.CreatedIssues.Should().HaveCount(1);

        // A second promote of the same issue must not create another GitHub issue.
        var exit = await RunWithGitHubAsync("promote", a);
        exit.Should().Be(0);
        GitHub.CreatedIssues.Should().HaveCount(1);
        Console.Output.Should().Contain("already promoted");
    }

    [Test]
    public async Task Absorb_creates_fleece_issue_and_comments_and_assigns_without_closing()
    {
        GitHub.AuthResult = new GitHubAuthResult
        {
            Authenticated = true, Login = "octocat", TokenSource = "fake", Repository = "owner/name",
        };
        GitHub.Issues.Add(new GitHubIssue { Number = 42, Title = "Upstream bug", Body = "Please fix" });

        var exit = await RunWithGitHubAsync("absorb", "#42");

        exit.Should().Be(0);

        var absorbed = LoadIssues().Single(i => i.Title == "Upstream bug");
        KeyedTag.HasKey(absorbed.Tags, "absorbed-from").Should().BeTrue();

        GitHub.Comments.Should().ContainSingle(c => c.Number == 42);
        GitHub.Assignments.Should().ContainSingle(c => c.Number == 42 && c.Login == "octocat");
    }

    [Test]
    public async Task Absorb_without_hash_performs_no_action_and_warns()
    {
        var exit = await RunWithGitHubAsync("absorb", "42");

        exit.Should().Be(1);
        GitHub.CreatedIssues.Should().BeEmpty();
        GitHub.Comments.Should().BeEmpty();
        LoadIssues().Should().BeEmpty();
        Console.Output.Should().Contain("#42");
    }
}
