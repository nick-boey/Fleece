using System.Reflection;
using System.Text;

namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("migrate")]
public class MigrateScenarios : CliScenarioTestBase
{
    private string IssuesDir => Path.Combine(BasePath, ".fleece", "issues");

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
    public async Task Migrate_converts_legacy_files_into_per_issue_logs()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "First"), LegacyIssueJson("i2", "Second"));
        WriteLegacyIssuesFile("bbb", LegacyIssueJson("i3", "Third"));

        var exit = await RunAsync("migrate");
        exit.Should().Be(0);

        var fleeceDir = Path.Combine(BasePath, ".fleece");
        Fs.File.Exists(Path.Combine(IssuesDir, "i1.jsonl")).Should().BeTrue();
        Fs.File.Exists(Path.Combine(IssuesDir, "i2.jsonl")).Should().BeTrue();
        Fs.File.Exists(Path.Combine(IssuesDir, "i3.jsonl")).Should().BeTrue();

        // v4 layout: no snapshot, no changes directory, legacy hashed files consumed.
        Fs.File.Exists(Path.Combine(fleeceDir, "issues.jsonl")).Should().BeFalse();
        Fs.Directory.Exists(Path.Combine(fleeceDir, "changes")).Should().BeFalse();
        Fs.Directory.GetFiles(fleeceDir, "issues_*.jsonl").Should().BeEmpty();

        LoadIssues().Select(i => i.Id).Should().BeEquivalentTo(["i1", "i2", "i3"]);
    }

    [Test]
    public async Task Migrate_is_idempotent_when_no_legacy_files_present()
    {
        Fs.Directory.CreateDirectory(IssuesDir);

        (await RunAsync("migrate")).Should().Be(0);
    }

    [Test]
    public async Task Migrate_adds_no_gitignore_entries()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Hello"));

        await RunAsync("migrate");

        var gitignorePath = Path.Combine(BasePath, ".gitignore");
        if (Fs.File.Exists(gitignorePath))
        {
            var gitignore = await Fs.File.ReadAllTextAsync(gitignorePath);
            gitignore.Should().NotContain(".fleece/.active-change");
            gitignore.Should().NotContain(".fleece/.replay-cache");
        }
    }

    [Test]
    public async Task Migrate_strips_per_property_timestamps()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Hello"));

        await RunAsync("migrate");

        var content = await Fs.File.ReadAllTextAsync(Path.Combine(IssuesDir, "i1.jsonl"));
        content.Should().NotContain("titleLastUpdate");
        content.Should().NotContain("statusLastUpdate");
        content.Should().NotContain("ModifiedBy");
    }

    [Test]
    public async Task Migrate_consumes_legacy_tombstone_files_without_writing_a_sidecar()
    {
        var fleeceDir = Path.Combine(BasePath, ".fleece");
        Fs.Directory.CreateDirectory(fleeceDir);
        await Fs.File.WriteAllTextAsync(Path.Combine(fleeceDir, "tombstones_aaa.jsonl"),
            """{"issueId":"t1","originalTitle":"Gone A","cleanedAt":"2026-04-01T10:00:00Z","cleanedBy":"alice"}""" + "\n");
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Stays"));

        await RunAsync("migrate");

        Fs.File.Exists(Path.Combine(fleeceDir, "tombstones.jsonl")).Should().BeFalse();
        Fs.File.Exists(Path.Combine(fleeceDir, "tombstones_aaa.jsonl")).Should().BeFalse();
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
    public async Task Migrate_diff_issues_fixture_reconciles_overlapping_issues()
    {
        var diffDir = GetExamplesDir("diff-issues");
        SeedFleeceDir(System.IO.Directory.GetFiles(diffDir, "issues_*.jsonl"));

        await RunAsync("migrate");

        var issues = LoadIssues();
        issues.Should().HaveCount(206);

        // Per-issue logs carry no per-property timestamp metadata.
        var anyLog = Fs.Directory.GetFiles(IssuesDir, "*.jsonl").First();
        var output = await Fs.File.ReadAllTextAsync(anyLog);
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
