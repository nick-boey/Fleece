namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("config")]
public class ConfigScenarios : CliScenarioTestBase
{
    [Test]
    public async Task Config_set_local_then_get_returns_value()
    {
        (await RunAsync("config", "--set", "identity=Alice")).Should().Be(0);
        (await RunAsync("config", "--get", "identity")).Should().Be(0);
        Stdout.Trim().Should().Be("Alice");
    }

    [Test]
    public async Task Config_list_renders_all_settings()
    {
        await RunAsync("config", "--set", "identity=Bob");
        Console.Clear(home: true);

        var exit = await RunAsync("config", "--list");
        exit.Should().Be(0);
        Console.Output.Should().Contain("identity");
        Console.Output.Should().Contain("autoMerge");
        Console.Output.Should().Contain("syncBranch");
    }

    [Test]
    public async Task Config_global_set_writes_to_global_path()
    {
        var exit = await RunAsync("config", "--global", "--set", "autoMerge=true");
        exit.Should().Be(0);

        var globalRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".fleece",
            "settings.json");
        Fs.File.Exists(globalRoot).Should().BeTrue();
    }

    [Test]
    public async Task Config_round_trip_through_effective_settings()
    {
        await RunAsync("config", "--set", "syncBranch=main");
        (await RunAsync("config", "--get", "syncBranch")).Should().Be(0);
        Stdout.Trim().Should().Be("main");
    }
}
