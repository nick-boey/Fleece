using System.Reflection;
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

    private void SeedFleeceDir(params string[] fixturePaths)
    {
        var fleeceDir = Path.Combine(BasePath, ".fleece");
        Fs.Directory.CreateDirectory(fleeceDir);
        foreach (var path in fixturePaths)
        {
            var content = System.IO.File.ReadAllText(path);
            var name = System.IO.Path.GetFileName(path);
            Fs.File.WriteAllText(System.IO.Path.Combine(fleeceDir, name), content);
        }
    }

    [Test]
    public async Task Migrate_diff_issues_fixture_creates_gitignore_and_changes_dir()
    {
        var diffDir = GetExamplesDir("diff-issues");
        SeedFleeceDir(System.IO.Directory.GetFiles(diffDir, "issues_*.jsonl"));

        await RunAsync("migrate");

        var gitignore = await Fs.File.ReadAllTextAsync(Path.Combine(BasePath, ".gitignore"));
        gitignore.Should().Contain(".fleece/.active-change");
        gitignore.Should().Contain(".fleece/.replay-cache");

        Fs.Directory.Exists(Path.Combine(BasePath, ".fleece", "changes")).Should().BeTrue();
    }

    [Test]
    public async Task Migrate_diff_issues_fixture_reconciles_overlapping_issues()
    {
        var diffDir = GetExamplesDir("diff-issues");
        SeedFleeceDir(System.IO.Directory.GetFiles(diffDir, "issues_*.jsonl"));

        await RunAsync("migrate");

        var issues = LoadIssues();
        issues.Should().HaveCount(206);

        var output = await Fs.File.ReadAllTextAsync(
            Path.Combine(BasePath, ".fleece", "issues.jsonl"));
        output.Should().NotContain("LastUpdate");
        output.Should().NotContain("ModifiedBy");
    }

    [Test]
    public async Task Migrate_nested_issues_fixture_preserves_parent_issues()
    {
        var nestedDir = GetExamplesDir("nested-issues");
        SeedFleeceDir(System.IO.Path.Combine(nestedDir, ".fleece", "issues_939e3c.jsonl"));

        await RunAsync("migrate");

        var issues = LoadIssues();
        issues.Should().HaveCount(13);

        var issue005 = issues.Single(i => i.Id == "ISSUE-005");
        issue005.ParentIssues.Should().Contain(p => p.ParentIssue == "ISSUE-004");
    }

    private static string GetExamplesDir(string subDir)
    {
        // Walk up from the test assembly to the repo root, then into tests/examples/.
        var dir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, ".git")))
        {
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        if (dir == null)
        {
            throw new InvalidOperationException("Could not find repo root from assembly location");
        }
        return System.IO.Path.Combine(dir, "tests", "examples", subDir);
    }
}
