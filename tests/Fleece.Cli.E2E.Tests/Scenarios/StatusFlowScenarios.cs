using Fleece.Core.Models;

namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("open")]
[Category("progress")]
[Category("review")]
[Category("complete")]
[Category("archived")]
[Category("closed")]
public class StatusFlowScenarios : CliScenarioTestBase
{
    private async Task<string> CreateIssueAsync(string title = "S")
    {
        await RunAsync("create", "-t", title, "-y", "task", "-d", "body");
        return LoadIssues().Single(i => i.Title == title).Id;
    }

    [TestCase("open", IssueStatus.Open)]
    [TestCase("progress", IssueStatus.Progress)]
    [TestCase("review", IssueStatus.Review)]
    [TestCase("complete", IssueStatus.Complete)]
    [TestCase("archived", IssueStatus.Archived)]
    [TestCase("closed", IssueStatus.Closed)]
    public async Task Status_alias_updates_issue(string alias, IssueStatus expected)
    {
        var id = await CreateIssueAsync($"status-{alias}");

        var exit = await RunAsync(alias, id);
        exit.Should().Be(0);

        LoadIssues().Single(i => i.Id == id).Status.Should().Be(expected);
    }

    [Test]
    public async Task Bulk_status_change_updates_multiple_issues()
    {
        var id1 = await CreateIssueAsync("bulk1");
        var id2 = await CreateIssueAsync("bulk2");

        var exit = await RunAsync("progress", id1, id2);
        exit.Should().Be(0);

        LoadIssues().Single(i => i.Id == id1).Status.Should().Be(IssueStatus.Progress);
        LoadIssues().Single(i => i.Id == id2).Status.Should().Be(IssueStatus.Progress);
    }

    [Test]
    public async Task Edit_with_status_flag_matches_alias_behavior()
    {
        var id = await CreateIssueAsync("edit-status");

        var exit = await RunAsync("edit", id, "-s", "complete");
        exit.Should().Be(0);

        LoadIssues().Single(i => i.Id == id).Status.Should().Be(IssueStatus.Complete);
    }
}
