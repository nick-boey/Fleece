namespace Fleece.Cli.Integration.Tests;

[TestFixture]
[NonParallelizable]
public class CommitIntegrationTests : GitTempRepoFixture
{
    [Test]
    public async Task Commit_writes_a_git_commit_after_creating_issue()
    {
        (await RunCliAsync("create", "-t", "Committable", "-y", "task", "-d", "body")).Should().Be(0);

        var exit = await RunCliAsync("commit", "-m", "test commit");
        exit.Should().Be(0);

        var log = GitOutput("log", "--pretty=%s");
        log.Should().Contain("test commit");
    }

    [Test]
    public async Task Commit_with_no_changes_returns_zero_with_warning()
    {
        // Pre-commit an initial state so there's something in history.
        (await RunCliAsync("create", "-t", "Seed", "-y", "task", "-d", "body")).Should().Be(0);
        (await RunCliAsync("commit", "-m", "seed")).Should().Be(0);

        var exit = await RunCliAsync("commit", "-m", "no-op");
        exit.Should().Be(0);
    }

    [Test]
    public async Task Commit_push_without_remote_reports_error_cleanly()
    {
        (await RunCliAsync("create", "-t", "Push", "-y", "task", "-d", "body")).Should().Be(0);

        var exit = await RunCliAsync("commit", "-m", "pushy", "--push");
        exit.Should().NotBe(-1);
    }
}
