using System.Text;
using Fleece.Core.EventSourcing.Services;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;

namespace Fleece.Cli.Integration.Tests;

/// <summary>
/// End-to-end lifecycle and merge-shape tests for event-sourced storage.
/// Each test exercises the full real-git workflow: branch switches, commits,
/// projection, and (simulated) squash-merge or multi-machine sessions.
/// </summary>
[TestFixture]
[NonParallelizable]
public class EventSourcedLifecycleTests : GitTempRepoFixture
{
    private string ChangesDir => Path.Combine(TempDir, ".fleece", "changes");
    private string SnapshotPath => Path.Combine(TempDir, ".fleece", "issues.jsonl");

    [Test]
    public async Task Lifecycle_create_commit_switch_branch_create_switch_back_replays_correctly()
    {
        (await RunCliAsync("create", "-t", "On main 1", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: first issue");

        // Capture the active-change pointer so the branch switch can simulate
        // a different machine writing on a feature branch.
        var pointerPath = Path.Combine(TempDir, ".fleece", ".active-change");
        var pointerBackup = File.ReadAllText(pointerPath);

        RunGit("checkout", "-b", "feature/x");
        // Pretend we're a fresh machine: drop the pointer so a new change file rotates.
        File.Delete(pointerPath);

        (await RunCliAsync("create", "-t", "On feature 1", "-y", "task", "-d", "x")).Should().Be(0);
        // Commit the feature change file so it becomes a tracked file on `feature/x`.
        // Otherwise git carries the uncommitted file through the checkout to main.
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature: add issue");

        // Capture state on feature branch.
        var listOnFeature = await ReadIssuesAsync();
        listOnFeature.Should().HaveCount(2);

        // Switch back to main and restore the original pointer.
        // Files committed on `feature/x` disappear from the working tree on checkout.
        RunGit("checkout", "main");
        File.WriteAllText(pointerPath, pointerBackup);

        var listOnMain = await ReadIssuesAsync();
        listOnMain.Should().HaveCount(1);
        listOnMain[0].Title.Should().Be("On main 1");
    }

    [Test]
    public async Task Squash_equivalence_branch_with_three_chained_change_files_replays_same_after_squash()
    {
        // Build a feature branch with three commits, each producing one change file.
        // Each event sets the same property to a different value; the last write wins.
        (await RunCliAsync("create", "-t", "Squashable", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: seed");

        var seedIssues = await ReadIssuesAsync();
        var id = seedIssues.Single().Id;

        RunGit("checkout", "-b", "feature/squash");
        // Three rotations on the feature branch -> three change files, each chained.
        await Rotate();
        (await RunCliAsync("edit", id, "-t", "First")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature: title=First");

        await Rotate();
        (await RunCliAsync("edit", id, "-t", "Second")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature: title=Second");

        await Rotate();
        (await RunCliAsync("edit", id, "-t", "Third")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature: title=Third");

        var preSquashIssues = await ReadIssuesAsync();
        preSquashIssues.Single().Title.Should().Be("Third");

        // Capture all change files produced by the feature branch so we can replay them post-squash.
        var preSquashChanges = Directory.GetFiles(ChangesDir).OrderBy(f => f).ToArray();
        preSquashChanges.Length.Should().BeGreaterThanOrEqualTo(4);

        // Simulate a squash-merge: take the change files at the tip of the feature branch,
        // squash them onto main as one commit, and verify replay produces the same final state.
        var changeFileContents = preSquashChanges
            .Select(p => (Path.GetFileName(p), File.ReadAllText(p)))
            .ToArray();

        RunGit("checkout", "main");
        // Wipe whatever main has under .fleece/changes (it should be empty here).
        if (Directory.Exists(ChangesDir))
        {
            foreach (var f in Directory.GetFiles(ChangesDir))
            {
                File.Delete(f);
            }
        }
        else
        {
            Directory.CreateDirectory(ChangesDir);
        }
        // "Squash" all feature change files onto main as if a squash-merge happened.
        foreach (var (name, body) in changeFileContents)
        {
            File.WriteAllText(Path.Combine(ChangesDir, name), body);
        }
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "squash: feature/squash");

        var postSquashIssues = await ReadIssuesAsync();
        postSquashIssues.Single().Title.Should().Be("Third");
    }

    [Test]
    public async Task Multi_machine_squash_with_chained_follows_pointers_replays_correctly()
    {
        // Machine 1 writes change_aaa with two events (title -> "foo" then -> "bar").
        // Machine 2 writes change_bbb with one event (title -> "baz") and follows="aaa".
        // Squash-merge to main; replay must produce title="baz".
        (await RunCliAsync("create", "-t", "Multi", "-y", "task", "-d", "x")).Should().Be(0);
        // Project the create event into the snapshot so we can reset .fleece/changes/
        // to an empty state and inject the manually-composed change files below.
        (await RunCliAsync("project")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "seed-projection");

        var seed = await ReadIssuesAsync();
        var id = seed.Single().Id;

        // Compose the two change files manually so we control the GUIDs and follows pointers.
        var changesDir = ChangesDir;
        Directory.CreateDirectory(changesDir);
        // Wipe any existing change files so we can start clean.
        foreach (var f in Directory.GetFiles(changesDir))
        {
            File.Delete(f);
        }

        var baseAt = "2026-04-01T10:00:00Z";
        File.WriteAllText(Path.Combine(changesDir, "change_aaa.jsonl"), string.Join('\n', new[]
        {
            """{"kind":"meta","follows":null}""",
            $$"""{"kind":"set","at":"{{baseAt}}","by":"m1","issueId":"{{id}}","property":"title","value":"foo"}""",
            $$"""{"kind":"set","at":"{{baseAt}}","by":"m1","issueId":"{{id}}","property":"title","value":"bar"}""",
            "",
        }));
        File.WriteAllText(Path.Combine(changesDir, "change_bbb.jsonl"), string.Join('\n', new[]
        {
            """{"kind":"meta","follows":"aaa"}""",
            $$"""{"kind":"set","at":"{{baseAt}}","by":"m2","issueId":"{{id}}","property":"title","value":"baz"}""",
            "",
        }));
        // Drop the active-change pointer so the next read doesn't try to use it.
        var pointerPath = Path.Combine(TempDir, ".fleece", ".active-change");
        if (File.Exists(pointerPath))
        {
            File.Delete(pointerPath);
        }
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "squash: multi-machine");

        var post = await ReadIssuesAsync();
        post.Single().Title.Should().Be("baz");
    }

    [Test]
    public async Task Project_after_squash_produces_state_matching_pre_squash_projection()
    {
        // Create + edit on main, capture pre-squash state, then run project.
        // The post-project state should match the pre-project state of the same in-memory dict
        // (this is the "project = pure compaction" invariant).
        (await RunCliAsync("create", "-t", "ProjAfterSquash", "-y", "task", "-d", "x")).Should().Be(0);
        var seed = await ReadIssuesAsync();
        var id = seed.Single().Id;
        (await RunCliAsync("edit", id, "-t", "Edited Title")).Should().Be(0);

        var preProject = await ReadIssuesAsync();
        preProject.Single().Title.Should().Be("Edited Title");

        (await RunCliAsync("project")).Should().Be(0);

        var postProject = await ReadIssuesAsync();
        postProject.Single().Id.Should().Be(id);
        postProject.Single().Title.Should().Be("Edited Title");

        // Change files are gone; snapshot carries the result.
        Directory.GetFiles(ChangesDir).Should().BeEmpty();
        File.Exists(SnapshotPath).Should().BeTrue();
    }

    [Test]
    public async Task Migrate_then_create_then_project_round_trip_stays_consistent()
    {
        // Plant legacy hashed files manually, run migrate-events, then exercise the new path.
        var fleeceDir = Path.Combine(TempDir, ".fleece");
        Directory.CreateDirectory(fleeceDir);
        await File.WriteAllTextAsync(
            Path.Combine(fleeceDir, "issues_aaa.jsonl"),
            """{"id":"old1","title":"Legacy","titleLastUpdate":"2026-04-01T10:00:00Z","status":"open","statusLastUpdate":"2026-04-01T10:00:00Z","type":"task","typeLastUpdate":"2026-04-01T10:00:00Z","createdAt":"2026-03-01T10:00:00Z","lastUpdate":"2026-04-01T10:00:00Z"}""" + "\n",
            Encoding.UTF8);

        (await RunCliAsync("migrate-events")).Should().Be(0);

        var afterMigrate = await ReadIssuesAsync();
        afterMigrate.Single().Title.Should().Be("Legacy");

        (await RunCliAsync("create", "-t", "Fresh", "-y", "task", "-d", "x")).Should().Be(0);
        var afterCreate = await ReadIssuesAsync();
        afterCreate.Should().HaveCount(2);

        (await RunCliAsync("project")).Should().Be(0);
        var afterProject = await ReadIssuesAsync();
        afterProject.Should().HaveCount(2);
        Directory.GetFiles(ChangesDir).Should().BeEmpty();
    }

    private async Task Rotate()
    {
        // Force the next write to produce a new change file by deleting the active-change pointer.
        var pointer = Path.Combine(TempDir, ".fleece", ".active-change");
        if (File.Exists(pointer))
        {
            File.Delete(pointer);
        }
        await Task.CompletedTask;
    }

    private async Task<IReadOnlyList<Issue>> ReadIssuesAsync()
    {
        var snapshot = new SnapshotStore(TempDir);
        var eventStore = new EventStore(TempDir);
        var replay = new ReplayEngine(eventStore);

        var initial = await snapshot.LoadSnapshotAsync();
        var changeFiles = await eventStore.GetAllChangeFilePathsAsync();
        if (changeFiles.Count == 0)
        {
            return initial.Values.ToList();
        }
        var state = await replay.ReplayAsync(initial, changeFiles, NullEventGitContext.Instance);
        return state.Values.ToList();
    }
}
