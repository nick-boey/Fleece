using System.Text;

namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("migrate")]
public class MigrateScenarios : CliScenarioTestBase
{
    private void WriteLegacyIssuesFile(string hash, params string[] jsonLines)
    {
        var dir = Path.Combine(BasePath, ".fleece");
        Fs.Directory.CreateDirectory(dir);
        var content = string.Join('\n', jsonLines) + "\n";
        Fs.File.WriteAllText(Path.Combine(dir, $"issues_{hash}.jsonl"), content, Encoding.UTF8);
    }

    private static string LegacyIssueJson(string id, string title, string lastUpdate = "2026-04-01T10:00:00Z") =>
        $$"""
        {"id":"{{id}}","title":"{{title}}","titleLastUpdate":"{{lastUpdate}}","status":"open","statusLastUpdate":"{{lastUpdate}}","type":"task","typeLastUpdate":"{{lastUpdate}}","createdAt":"2026-03-01T10:00:00Z","lastUpdate":"{{lastUpdate}}"}
        """;

    [Test]
    public async Task Migrate_converts_legacy_files_into_new_layout()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "First"), LegacyIssueJson("i2", "Second"));
        WriteLegacyIssuesFile("bbb", LegacyIssueJson("i3", "Third"));

        var exit = await RunAsync("migrate");
        exit.Should().Be(0);

        var fleeceDir = Path.Combine(BasePath, ".fleece");
        Fs.File.Exists(Path.Combine(fleeceDir, "issues.jsonl")).Should().BeTrue();
        Fs.Directory.Exists(Path.Combine(fleeceDir, "changes")).Should().BeTrue();
        Fs.Directory.GetFiles(fleeceDir, "issues_*.jsonl").Should().BeEmpty();
    }

    [Test]
    public async Task Migrate_is_idempotent_on_already_migrated_repo()
    {
        var fleeceDir = Path.Combine(BasePath, ".fleece");
        Fs.Directory.CreateDirectory(fleeceDir);
        await Fs.File.WriteAllTextAsync(Path.Combine(fleeceDir, "issues.jsonl"), "");

        (await RunAsync("migrate")).Should().Be(0);
    }

    [Test]
    public async Task Migrate_adds_gitignore_entries()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Hello"));

        await RunAsync("migrate");

        var gitignore = await Fs.File.ReadAllTextAsync(Path.Combine(BasePath, ".gitignore"));
        gitignore.Should().Contain(".fleece/.active-change");
        gitignore.Should().Contain(".fleece/.replay-cache");
    }

    [Test]
    public async Task Migrate_strips_per_property_timestamps_from_snapshot()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Hello"));

        await RunAsync("migrate");

        var content = await Fs.File.ReadAllTextAsync(Path.Combine(BasePath, ".fleece", "issues.jsonl"));
        content.Should().NotContain("titleLastUpdate");
        content.Should().NotContain("statusLastUpdate");
        content.Should().NotContain("ModifiedBy");
    }

    [Test]
    public async Task Migrate_unions_tombstones()
    {
        var fleeceDir = Path.Combine(BasePath, ".fleece");
        Fs.Directory.CreateDirectory(fleeceDir);
        await Fs.File.WriteAllTextAsync(Path.Combine(fleeceDir, "tombstones_aaa.jsonl"),
            """{"issueId":"t1","originalTitle":"Gone A","cleanedAt":"2026-04-01T10:00:00Z","cleanedBy":"alice"}""" + "\n");
        await Fs.File.WriteAllTextAsync(Path.Combine(fleeceDir, "tombstones_bbb.jsonl"),
            """{"issueId":"t2","originalTitle":"Gone B","cleanedAt":"2026-04-02T10:00:00Z","cleanedBy":"bob"}""" + "\n");
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Stays"));

        await RunAsync("migrate");

        var content = await Fs.File.ReadAllTextAsync(Path.Combine(fleeceDir, "tombstones.jsonl"));
        content.Should().Contain("t1");
        content.Should().Contain("t2");
        Fs.File.Exists(Path.Combine(fleeceDir, "tombstones_aaa.jsonl")).Should().BeFalse();
        Fs.File.Exists(Path.Combine(fleeceDir, "tombstones_bbb.jsonl")).Should().BeFalse();
    }

    [Test]
    public async Task Migrate_events_subcommand_is_not_recognised()
    {
        var exit = await RunAsync("migrate-events");
        exit.Should().NotBe(0);
    }
}
