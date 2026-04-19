using Fleece.Core.Models;

namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("create")]
[Category("show")]
[Category("list")]
[Category("edit")]
[Category("delete")]
public class CrudScenarios : CliScenarioTestBase
{
    [Test]
    public async Task Create_happy_path_writes_issue_to_jsonl()
    {
        var exit = await RunAsync("create", "-t", "First issue", "-y", "bug", "-d", "Broken login");
        exit.Should().Be(0);

        var issues = LoadIssues();
        issues.Should().ContainSingle();
        issues[0].Title.Should().Be("First issue");
        issues[0].Type.Should().Be(IssueType.Bug);
        issues[0].Description.Should().Be("Broken login");
    }

    [Test]
    public async Task Show_by_id_returns_issue_json()
    {
        await RunAsync("create", "-t", "Find me", "-y", "task");
        var id = LoadIssues()[0].Id;

        var exit = await RunAsync("show", id, "--json");
        exit.Should().Be(0);
        ParseJsonOutput().GetProperty("issue").GetProperty("id").GetString().Should().Be(id);
    }

    [Test]
    public async Task List_table_contains_created_issue()
    {
        await RunAsync("create", "-t", "Table item", "-y", "task", "-d", "body");
        Console.Clear(home: true);

        var exit = await RunAsync("list");
        exit.Should().Be(0);
        Console.Output.Should().Contain("Table item");
    }

    [Test]
    public async Task List_json_returns_array()
    {
        await RunAsync("create", "-t", "A", "-y", "bug", "-d", "desc");
        await RunAsync("create", "-t", "B", "-y", "task", "-d", "desc");

        var exit = await RunAsync("list", "--json");
        exit.Should().Be(0);
        ParseJsonOutput().GetArrayLength().Should().Be(2);
    }

    [Test]
    public async Task Edit_updates_each_field()
    {
        await RunAsync("create", "-t", "Old title", "-y", "bug", "-d", "old");
        var id = LoadIssues()[0].Id;

        (await RunAsync("edit", id, "-t", "New title")).Should().Be(0);
        (await RunAsync("edit", id, "-d", "new desc")).Should().Be(0);
        (await RunAsync("edit", id, "-s", "progress")).Should().Be(0);
        (await RunAsync("edit", id, "-y", "task")).Should().Be(0);
        (await RunAsync("edit", id, "-p", "2")).Should().Be(0);

        var updated = LoadIssues().Single(i => i.Id == id);
        updated.Title.Should().Be("New title");
        updated.Description.Should().Be("new desc");
        updated.Status.Should().Be(IssueStatus.Progress);
        updated.Type.Should().Be(IssueType.Task);
        updated.Priority.Should().Be(2);
    }

    [Test]
    public async Task Delete_removes_issue_from_active_list()
    {
        await RunAsync("create", "-t", "To delete", "-y", "task", "-d", "x");
        var id = LoadIssues()[0].Id;

        var exit = await RunAsync("delete", id);
        exit.Should().Be(0);

        var after = LoadIssues().Single(i => i.Id == id);
        after.Status.Should().Be(IssueStatus.Deleted);
    }

    [Test]
    public async Task Show_after_delete_soft_succeeds_but_unknown_id_errors()
    {
        var exit = await RunAsync("show", "nonexistent");
        exit.Should().Be(1);
        Console.Output.Should().Contain("not found");
    }
}
