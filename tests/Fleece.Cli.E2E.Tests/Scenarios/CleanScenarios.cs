using Fleece.Core.Models;

namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("clean")]
public class CleanScenarios : CliScenarioTestBase
{
    [Test]
    public async Task Delete_then_clean_produces_tombstone_and_removes_issue()
    {
        await RunAsync("create", "-t", "Gone", "-y", "task", "-d", "body");
        var id = LoadIssues().Single().Id;
        await RunAsync("delete", id);

        var exit = await RunAsync("clean");
        exit.Should().Be(0);

        LoadIssues().Should().NotContain(i => i.Id == id);
        LoadTombstones().Should().ContainSingle(t => t.IssueId == id);
    }

    [Test]
    public async Task Clean_no_strip_refs_preserves_linked_references()
    {
        await RunAsync("create", "-t", "Victim", "-y", "task", "-d", "body");
        var victimId = LoadIssues().Single(i => i.Title == "Victim").Id;
        await RunAsync("create", "-t", "Linker", "-y", "task", "-d", "body", "--linked-issues", victimId);
        var linkerId = LoadIssues().Single(i => i.Title == "Linker").Id;

        await RunAsync("delete", victimId);
        await RunAsync("clean", "--no-strip-refs");

        LoadIssues().Single(i => i.Id == linkerId).LinkedIssues.Should().Contain(victimId);
    }

    [Test]
    public async Task Clean_include_complete_removes_complete_issues()
    {
        await RunAsync("create", "-t", "Done", "-y", "task", "-d", "body", "-s", "complete");
        var id = LoadIssues().Single().Id;

        var exit = await RunAsync("clean", "--include-complete");
        exit.Should().Be(0);
        LoadIssues().Should().NotContain(i => i.Id == id);
    }

    [Test]
    public async Task Clean_include_archived_removes_archived_issues()
    {
        await RunAsync("create", "-t", "Arc", "-y", "task", "-d", "body", "-s", "archived");
        var id = LoadIssues().Single().Id;

        var exit = await RunAsync("clean", "--include-archived");
        exit.Should().Be(0);
        LoadIssues().Should().NotContain(i => i.Id == id);
    }

    [Test]
    public async Task Clean_include_closed_removes_closed_issues()
    {
        await RunAsync("create", "-t", "Cls", "-y", "task", "-d", "body", "-s", "closed");
        var id = LoadIssues().Single().Id;

        var exit = await RunAsync("clean", "--include-closed");
        exit.Should().Be(0);
        LoadIssues().Should().NotContain(i => i.Id == id);
    }

    [Test]
    public async Task Clean_dry_run_leaves_issue_and_tombstones_untouched()
    {
        await RunAsync("create", "-t", "Victim", "-y", "task", "-d", "body");
        var id = LoadIssues().Single().Id;
        await RunAsync("delete", id);

        var exit = await RunAsync("clean", "--dry-run");
        exit.Should().Be(0);

        LoadIssues().Single(i => i.Id == id).Status.Should().Be(IssueStatus.Deleted);
        LoadTombstones().Should().BeEmpty();
    }
}
