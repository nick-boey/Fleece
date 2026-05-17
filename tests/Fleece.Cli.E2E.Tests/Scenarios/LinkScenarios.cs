namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("link")]
public class LinkScenarios : CliScenarioTestBase
{
    [Test]
    public async Task Link_merge_no_merge_in_progress_exits_zero_and_writes_nothing()
    {
        Fs.Directory.CreateDirectory(Path.Combine(BasePath, ".fleece", "changes"));

        var exit = await RunAsync("link", "--merge");
        exit.Should().Be(0);

        Fs.Directory.GetFiles(Path.Combine(BasePath, ".fleece", "changes")).Should().BeEmpty();
    }

    [Test]
    public async Task Link_without_merge_flag_errors()
    {
        var exit = await RunAsync("link");
        exit.Should().NotBe(0);
    }
}
