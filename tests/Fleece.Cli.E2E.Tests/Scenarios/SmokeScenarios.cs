namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
public class SmokeScenarios : CliScenarioTestBase
{
    [Test]
    public async Task Create_then_list_json_returns_issue()
    {
        var createExit = await RunAsync("create", "-t", "Bug X", "-y", "bug");
        createExit.Should().Be(0);

        LoadIssues().Should().ContainSingle(i => i.Title == "Bug X");
    }
}
