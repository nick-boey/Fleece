namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("dependency")]
public class DependencyScenarios : CliScenarioTestBase
{
    private async Task<(string parent, string child)> CreateTwoAsync()
    {
        await RunAsync("create", "-t", "P", "-y", "task", "-d", "parent body");
        await RunAsync("create", "-t", "C", "-y", "task", "-d", "child body");
        var all = LoadIssues();
        return (all.Single(i => i.Title == "P").Id, all.Single(i => i.Title == "C").Id);
    }

    [Test]
    public async Task Add_dependency_sets_parent()
    {
        var (parent, child) = await CreateTwoAsync();

        var exit = await RunAsync("dependency", "--parent", parent, "--child", child);
        exit.Should().Be(0);

        LoadIssues().Single(i => i.Id == child).ParentIssues[0].ParentIssue.Should().Be(parent);
    }

    [Test]
    public async Task Remove_dependency_marks_parent_ref_inactive()
    {
        var (parent, child) = await CreateTwoAsync();
        await RunAsync("dependency", "--parent", parent, "--child", child);

        var exit = await RunAsync("dependency", "--parent", parent, "--child", child, "--remove");
        exit.Should().Be(0);

        var childIssue = LoadIssues().Single(i => i.Id == child);
        childIssue.ParentIssues.Where(p => p.Active).Should().BeEmpty();
    }

    [Test]
    public async Task First_positioning_places_before_existing_siblings()
    {
        var (parent, child1) = await CreateTwoAsync();
        await RunAsync("dependency", "--parent", parent, "--child", child1);

        await RunAsync("create", "-t", "Sib", "-y", "task", "-d", "s");
        var sibId = LoadIssues().Single(i => i.Title == "Sib").Id;

        var exit = await RunAsync("dependency", "--parent", parent, "--child", sibId, "--first");
        exit.Should().Be(0);

        LoadIssues().Single(i => i.Id == sibId).ParentIssues[0].ParentIssue.Should().Be(parent);
    }

    [Test]
    public async Task Cycle_is_rejected()
    {
        var (parent, child) = await CreateTwoAsync();
        await RunAsync("dependency", "--parent", parent, "--child", child);

        var exit = await RunAsync("dependency", "--parent", child, "--child", parent);
        exit.Should().Be(1);
    }
}
