namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("list")]
public class HierarchyScenarios : CliScenarioTestBase
{
    [Test]
    public async Task Parent_child_create_records_parent_reference()
    {
        await RunAsync("create", "-t", "Parent", "-y", "task", "-d", "p");
        var parentId = LoadIssues().Single().Id;

        await RunAsync("create", "-t", "Child", "-y", "task", "-d", "c", "--parent-issues", parentId);
        var child = LoadIssues().Single(i => i.Title == "Child");
        child.ParentIssues.Should().ContainSingle();
        child.ParentIssues[0].ParentIssue.Should().Be(parentId);
    }

    [Test]
    public async Task List_tree_renders_without_error()
    {
        await RunAsync("create", "-t", "Root", "-y", "task", "-d", "p");
        var parentId = LoadIssues().Single().Id;
        await RunAsync("create", "-t", "Leaf", "-y", "task", "-d", "c", "--parent-issues", parentId);

        var exit = await RunAsync("list", "--tree");
        exit.Should().Be(0);
        Console.Output.Should().Contain("Root");
        Console.Output.Should().Contain("Leaf");
    }

    [Test]
    public async Task List_next_renders_ordered_graph()
    {
        await RunAsync("create", "-t", "P", "-y", "task", "-d", "p");
        var parentId = LoadIssues().Single().Id;
        await RunAsync("create", "-t", "Ch", "-y", "task", "-d", "c", "--parent-issues", parentId);

        var exit = await RunAsync("list", "--next");
        exit.Should().Be(0);
    }

    [Test]
    public async Task Orphan_parent_reference_is_tolerated_in_list()
    {
        await RunAsync("create", "-t", "Lonely", "-y", "task", "-d", "body", "--parent-issues", "deadbe");

        var exit = await RunAsync("list");
        exit.Should().Be(0);
        Console.Output.Should().Contain("Lonely");
    }
}
