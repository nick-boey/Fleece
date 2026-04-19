namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("search")]
public class SearchScenarios : CliScenarioTestBase
{
    [SetUp]
    public async Task SeedAsync()
    {
        await RunAsync("create", "-t", "Login bug", "-y", "bug", "-d", "user cannot login");
        await RunAsync("create", "-t", "Improve docs", "-y", "task", "-d", "README updates and homepage");
        await RunAsync("create", "-t", "Other", "-y", "chore", "-d", "random");
    }

    [Test]
    public async Task Search_by_title_match_returns_hit()
    {
        var exit = await RunAsync("search", "login", "--json");
        exit.Should().Be(0);

        var arr = ParseJsonOutput();
        arr.GetArrayLength().Should().BeGreaterThan(0);
        arr.EnumerateArray().Any(e => e.GetProperty("title").GetString()!.Contains("Login")).Should().BeTrue();
    }

    [Test]
    public async Task Search_by_description_match_returns_hit()
    {
        var exit = await RunAsync("search", "README", "--json");
        exit.Should().Be(0);
        ParseJsonOutput().GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task Search_no_results_returns_empty_array()
    {
        var exit = await RunAsync("search", "noSuchTokenWillMatch", "--json");
        exit.Should().Be(0);
        ParseJsonOutput().GetArrayLength().Should().Be(0);
    }
}
