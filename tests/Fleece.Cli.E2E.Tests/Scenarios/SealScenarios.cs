namespace Fleece.Cli.E2E.Tests.Scenarios;

/// <summary>
/// Exercises the <c>fleece seal</c> branch-lifecycle command: gating on active issues,
/// archiving + clearing when all are inactive, and the empty-set no-op.
/// </summary>
[TestFixture]
[Category("seal")]
public class SealScenarios : CliScenarioTestBase
{
    private async Task<string> CreateIssueAsync(string title)
    {
        await RunAsync("create", "-t", title, "-y", "task", "-d", "body");
        return LoadIssues().Single(i => i.Title == title).Id;
    }

    private string IssuesDir => Path.Combine(BasePath, ".fleece", "issues");
    private string ArchiveDir => Path.Combine(BasePath, ".fleece", "archive");

    [Test]
    public async Task Seal_refuses_while_an_active_issue_remains()
    {
        await CreateIssueAsync("Still open");

        var exit = await RunAsync("seal");

        exit.Should().Be(1);
        Console.Output.Should().Contain("Cannot seal");
        Fs.Directory.GetFiles(IssuesDir, "*.jsonl").Should().NotBeEmpty();
        Fs.Directory.Exists(ArchiveDir).Should().BeFalse();
    }

    [Test]
    public async Task Seal_archives_and_clears_when_all_inactive()
    {
        var a = await CreateIssueAsync("Done one");
        var b = await CreateIssueAsync("Done two");
        await RunAsync("complete", a);
        await RunAsync("closed", b);

        var exit = await RunAsync("seal");

        exit.Should().Be(0);
        Fs.Directory.GetFiles(IssuesDir, "*.jsonl").Should().BeEmpty();
        Fs.Directory.Exists(ArchiveDir).Should().BeTrue();
        Fs.Directory.GetFiles(ArchiveDir, "issues_*.jsonl").Should().HaveCount(1);
    }

    [Test]
    public async Task Seal_on_empty_issue_set_is_a_noop_success()
    {
        Fs.Directory.CreateDirectory(IssuesDir);

        var exit = await RunAsync("seal");

        exit.Should().Be(0);
        Fs.Directory.Exists(ArchiveDir).Should().BeFalse();
    }
}
