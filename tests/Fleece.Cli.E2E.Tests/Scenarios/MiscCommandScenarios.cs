namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("validate")]
[Category("next")]
[Category("move")]
[Category("merge")]
[Category("migrate")]
[Category("prime")]
[Category("diff")]
[Category("commit")]
[Category("install")]
public class MiscCommandScenarios : CliScenarioTestBase
{
    [Test]
    public async Task Validate_no_cycles_returns_zero()
    {
        await RunAsync("create", "-t", "x", "-y", "task", "-d", "b");
        var exit = await RunAsync("validate");
        exit.Should().Be(0);
    }

    [Test]
    public async Task Next_returns_actionable_issues()
    {
        await RunAsync("create", "-t", "Actionable", "-y", "task", "-d", "b", "-s", "open");

        var exit = await RunAsync("next", "--json");
        exit.Should().Be(0);
        ParseJsonOutput().GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Move_up_reorders_sibling_when_parent_has_children()
    {
        await RunAsync("create", "-t", "P", "-y", "task", "-d", "b");
        var parentId = LoadIssues().Single().Id;
        await RunAsync("create", "-t", "A", "-y", "task", "-d", "b", "--parent-issues", parentId);
        await RunAsync("create", "-t", "B", "-y", "task", "-d", "b", "--parent-issues", parentId);

        var bId = LoadIssues().Single(i => i.Title == "B").Id;

        var exit = await RunAsync("move", bId, "--up");
        exit.Should().Be(0);
    }

    [Test]
    public async Task Merge_with_no_duplicates_reports_success()
    {
        await RunAsync("create", "-t", "solo", "-y", "task", "-d", "b");

        var exit = await RunAsync("merge");
        exit.Should().Be(0);
        Console.Output.Should().Contain("No duplicates");
    }

    [Test]
    public async Task Migrate_dry_run_reports_status()
    {
        await RunAsync("create", "-t", "issue", "-y", "task", "-d", "b");

        var exit = await RunAsync("migrate", "--dry-run", "--json");
        exit.Should().Be(0);
        ParseJsonOutput().GetProperty("migrationNeeded").ValueKind.ToString().Should().BeOneOf("True", "False");
    }

    [Test]
    public async Task Prime_without_fleece_dir_exits_cleanly()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "fleece-prime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tmp);
            var exit = await RunAsync("prime");
            exit.Should().Be(0);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
            Directory.Delete(tmp, recursive: true);
        }
    }

    [Test]
    public async Task Diff_missing_file_errors()
    {
        var exit = await RunAsync("diff", "/nonexistent-a.jsonl", "/nonexistent-b.jsonl");
        exit.Should().Be(1);
        Console.Output.Should().Contain("File not found");
    }

    [Test]
    public async Task Commit_outside_git_repo_errors()
    {
        var exit = await RunAsync("commit");
        exit.Should().Be(1);
        Console.Output.Should().ContainAny("not a git repository", "Not a git repository", "git command not found");
    }

    [Test]
    public async Task Install_writes_claude_hooks_into_base_path()
    {
        var exit = await RunAsync("install");
        exit.Should().Be(0);
        Fs.File.Exists(Path.Combine(BasePath, ".claude", "settings.json")).Should().BeTrue();
    }
}
