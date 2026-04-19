namespace Fleece.Cli.Integration.Tests;

[TestFixture]
[NonParallelizable]
public class SmokeIntegrationTests : GitTempRepoFixture
{
    [Test]
    public async Task List_json_against_real_temp_dir_returns_populated_array()
    {
        (await RunCliAsync("create", "-t", "Real issue", "-y", "task", "-d", "body")).Should().Be(0);

        var exit = await RunCliAsync("list", "--json");
        exit.Should().Be(0);
    }
}
