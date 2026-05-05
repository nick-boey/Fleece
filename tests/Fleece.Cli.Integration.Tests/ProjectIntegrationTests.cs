using Fleece.Core.EventSourcing.Services;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;

namespace Fleece.Cli.Integration.Tests;

[TestFixture]
[NonParallelizable]
public class ProjectIntegrationTests : GitTempRepoFixture
{
    [Test]
    public async Task Project_compacts_change_files_into_snapshot_on_main()
    {
        (await RunCliAsync("create", "-t", "First", "-y", "task", "-d", "x")).Should().Be(0);
        (await RunCliAsync("create", "-t", "Second", "-y", "task", "-d", "y")).Should().Be(0);

        Directory.GetFiles(Path.Combine(TempDir, ".fleece", "changes"))
            .Should().NotBeEmpty();

        var exit = await RunCliAsync("project");
        exit.Should().Be(0);

        Directory.GetFiles(Path.Combine(TempDir, ".fleece", "changes"))
            .Should().BeEmpty();

        var snapshotPath = Path.Combine(TempDir, ".fleece", "issues.jsonl");
        File.Exists(snapshotPath).Should().BeTrue();
        var lines = File.ReadAllLines(snapshotPath);
        lines.Should().HaveCount(2);
    }

    [Test]
    public async Task Project_refuses_to_run_off_default_branch()
    {
        (await RunCliAsync("create", "-t", "Onmain", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".");
        RunGit("commit", "-m", "seed");
        RunGit("checkout", "-b", "feature/sandbox");

        var exit = await RunCliAsync("project");
        exit.Should().Be(1);
    }

    [Test]
    public async Task Project_is_idempotent_when_no_events_remain()
    {
        (await RunCliAsync("create", "-t", "Once", "-y", "task", "-d", "x")).Should().Be(0);
        (await RunCliAsync("project")).Should().Be(0);

        var snapshotPath = Path.Combine(TempDir, ".fleece", "issues.jsonl");
        var snapshotFirst = File.ReadAllText(snapshotPath);

        (await RunCliAsync("project")).Should().Be(0);

        File.ReadAllText(snapshotPath).Should().Be(snapshotFirst);
        Directory.GetFiles(Path.Combine(TempDir, ".fleece", "changes"))
            .Should().BeEmpty();
    }

    [Test]
    public async Task Project_auto_cleans_soft_deleted_issues_older_than_30_days()
    {
        // Seed a snapshot directly with a stale Deleted issue (LastUpdate >30d in the past).
        var fleeceDir = Path.Combine(TempDir, ".fleece");
        Directory.CreateDirectory(fleeceDir);
        var snapshot = new SnapshotStore(TempDir);
        var stale = new Issue
        {
            Id = "stale1",
            Title = "Old Forgotten",
            Status = IssueStatus.Deleted,
            Type = IssueType.Task,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-60),
            LastUpdate = DateTimeOffset.UtcNow.AddDays(-31),
        };
        await snapshot.WriteSnapshotAsync(new Dictionary<string, Issue> { ["stale1"] = stale });

        var exit = await RunCliAsync("project");
        exit.Should().Be(0);

        var snapshotLines = File.ReadAllLines(Path.Combine(fleeceDir, "issues.jsonl"));
        snapshotLines.Should().BeEmpty();

        File.Exists(Path.Combine(fleeceDir, "tombstones.jsonl")).Should().BeTrue();
        var tombs = File.ReadAllLines(Path.Combine(fleeceDir, "tombstones.jsonl"));
        tombs.Should().HaveCount(1);
        tombs[0].Should().Contain("\"issueId\":\"stale1\"");
        tombs[0].Should().Contain("\"originalTitle\":\"Old Forgotten\"");
    }

    [Test]
    public async Task Project_leaves_recently_deleted_issues_intact()
    {
        var fleeceDir = Path.Combine(TempDir, ".fleece");
        Directory.CreateDirectory(fleeceDir);
        var snapshot = new SnapshotStore(TempDir);
        var fresh = new Issue
        {
            Id = "fresh1",
            Title = "Just deleted",
            Status = IssueStatus.Deleted,
            Type = IssueType.Task,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastUpdate = DateTimeOffset.UtcNow.AddHours(-2),
        };
        await snapshot.WriteSnapshotAsync(new Dictionary<string, Issue> { ["fresh1"] = fresh });

        (await RunCliAsync("project")).Should().Be(0);

        var snapshotLines = File.ReadAllLines(Path.Combine(fleeceDir, "issues.jsonl"));
        snapshotLines.Should().HaveCount(1);
        // The tombstones sidecar may exist (rewritten by projection) but must contain no entries.
        var tombsPath = Path.Combine(fleeceDir, "tombstones.jsonl");
        if (File.Exists(tombsPath))
        {
            File.ReadAllText(tombsPath).Trim().Should().BeEmpty();
        }
    }

    [Test]
    public async Task Project_stages_resulting_files_for_user_to_commit()
    {
        (await RunCliAsync("create", "-t", "ToProject", "-y", "task", "-d", "x")).Should().Be(0);

        (await RunCliAsync("project")).Should().Be(0);

        var status = GitOutput("status", "--porcelain", ".fleece/");
        status.Should().Contain(".fleece/issues.jsonl");
    }

    [Test]
    public async Task Merge_prints_deprecation_notice_to_stderr()
    {
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            WorkingDirectory = TempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Fleece.Cli", "Fleece.Cli.csproj"));
        psi.ArgumentList.Add("--no-build");
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add("merge");
        psi.ArgumentList.Add("--dry-run");

        // We can shortcut: just call the command's executor and check stderr capture.
        // Since spawning dotnet run is heavyweight, instead use Console.Error redirection.
        var sw = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(sw);
        try
        {
            await RunCliAsync("merge", "--dry-run");
        }
        finally
        {
            Console.SetError(originalErr);
        }

        sw.ToString().Should().Contain("deprecated").And.Contain("fleece project");
    }
}
