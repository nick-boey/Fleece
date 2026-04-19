namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("create")]
[Category("show")]
public class ErrorPathScenarios : CliScenarioTestBase
{
    [Test]
    public async Task Create_missing_title_errors()
    {
        var exit = await RunAsync("create", "-y", "bug");
        exit.Should().Be(1);
        Console.Output.Should().Contain("--title is required");
    }

    [Test]
    public async Task Create_missing_type_errors()
    {
        var exit = await RunAsync("create", "-t", "No type");
        exit.Should().Be(1);
        Console.Output.Should().Contain("--type is required");
    }

    [Test]
    public async Task Create_invalid_type_errors()
    {
        var exit = await RunAsync("create", "-t", "X", "-y", "notarealtype");
        exit.Should().Be(1);
        Console.Output.Should().Contain("Invalid type");
    }

    [Test]
    public async Task Show_unknown_id_errors()
    {
        var exit = await RunAsync("show", "zzzzzz");
        exit.Should().Be(1);
        Console.Output.Should().Contain("not found");
    }

    [Test]
    public async Task Create_empty_parent_issues_still_creates_without_parent()
    {
        var exit = await RunAsync("create", "-t", "Has", "-y", "task", "-d", "b", "--parent-issues", " ");
        exit.Should().Be(0);
        LoadIssues().Single().ParentIssues.Should().BeEmpty();
    }
}
