using System.Text;
using Fleece.Core.EventSourcing.Services;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;
using Fleece.Core.Services;

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
    public async Task Merge_two_branches_editing_different_properties_on_same_issue_replays_both_changes()
    {
        (await RunCliAsync("create", "-t", "Merge diff props", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: seed");

        var seed = await ReadIssuesAsync();
        var id = seed.Single().Id;

        // Branch A: edit title
        var pointerPath = Path.Combine(TempDir, ".fleece", ".active-change");
        var pointerBackup = File.ReadAllText(pointerPath);

        RunGit("checkout", "-b", "feature/a");
        File.Delete(pointerPath);
        (await RunCliAsync("edit", id, "-t", "Title from A")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature/a: edit title");

        // Branch B (from same base): edit status
        RunGit("checkout", "main");
        File.WriteAllText(pointerPath, pointerBackup);
        RunGit("checkout", "-b", "feature/b");
        File.Delete(pointerPath);
        (await RunCliAsync("edit", id, "-s", "Complete")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature/b: edit status");

        // Merge feature/b into feature/a
        RunGit("checkout", "feature/a");
        RunGit("merge", "feature/b", "--no-edit");

        var issues = await ReadIssuesAsync();
        var merged = issues.Single(i => i.Id == id);
        merged.Title.Should().Be("Title from A");
        merged.Status.Should().Be(IssueStatus.Complete);
    }

    [Test]
    public async Task Merge_two_branches_editing_same_property_on_same_issue_last_commit_wins()
    {
        // Use explicit commit timestamps so the commit-ordinal tiebreak is deterministic.
        // Otherwise A and B can land in the same second and `git rev-list --date-order`
        // is free to order them either way, flaking the assertion.
        var seedDate = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var aDate = new DateTimeOffset(2026, 5, 1, 11, 0, 0, TimeSpan.Zero);
        var bDate = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

        (await RunCliAsync("create", "-t", "Merge same prop", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGitCommit("main: seed", seedDate);

        var seed = await ReadIssuesAsync();
        var id = seed.Single().Id;

        var pointerPath = Path.Combine(TempDir, ".fleece", ".active-change");
        var pointerBackup = File.ReadAllText(pointerPath);

        // Branch A: set title to "A"
        RunGit("checkout", "-b", "feature/a");
        File.Delete(pointerPath);
        (await RunCliAsync("edit", id, "-t", "A")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGitCommit("feature/a: title=A", aDate);

        // Branch B: set title to "B"
        RunGit("checkout", "main");
        File.WriteAllText(pointerPath, pointerBackup);
        RunGit("checkout", "-b", "feature/b");
        File.Delete(pointerPath);
        (await RunCliAsync("edit", id, "-t", "B")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGitCommit("feature/b: title=B", bDate);

        // Merge feature/b into feature/a (B committed after A → B's events should win)
        RunGit("checkout", "feature/a");
        RunGit("merge", "feature/b", "--no-edit");

        var issues = await ReadIssuesAsync();
        var merged = issues.Single(i => i.Id == id);
        merged.Title.Should().Be("B");
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
    public async Task Real_squash_merge_produces_equivalent_state_after_project()
    {
        // Build a feature branch with chained change files, then real git merge --squash.
        // The post-squash + project state should match the pre-squash state.
        (await RunCliAsync("create", "-t", "SquashMe", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: seed");

        var seed = await ReadIssuesAsync();
        var id = seed.Single().Id;

        RunGit("checkout", "-b", "feature/squash-real");
        // Three changes on the feature branch
        await Rotate();
        (await RunCliAsync("edit", id, "-t", "v1")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature: title=v1");

        await Rotate();
        (await RunCliAsync("edit", id, "-t", "v2")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature: title=v2");

        await Rotate();
        (await RunCliAsync("edit", id, "-t", "v3")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature: title=v3");

        var preSquash = await ReadIssuesAsync();
        preSquash.Single().Title.Should().Be("v3");

        // Real squash-merge onto main
        RunGit("checkout", "main");
        RunGit("merge", "--squash", "feature/squash-real");
        RunGit("commit", "-m", "squash: feature/squash-real");

        // Run project to compact
        (await RunCliAsync("project")).Should().Be(0);

        var postProject = await ReadIssuesAsync();
        postProject.Single().Title.Should().Be("v3");
    }

    [Test]
    public async Task Parallel_branches_with_no_follows_ordered_by_commit_ordinal()
    {
        // Seed: create an issue, project to clear change files, commit.
        (await RunCliAsync("create", "-t", "Ordinal test", "-y", "task", "-d", "x")).Should().Be(0);
        (await RunCliAsync("project")).Should().Be(0);
        RunGit("add", ".fleece");

        var seed = await ReadIssuesAsync();
        var id = seed.Single().Id;

        // Manually plant two change files (no follows) in separate commits.
        var changesDir = ChangesDir;
        Directory.CreateDirectory(changesDir);

        // First commit: change_aaa (sets title to "early")
        File.WriteAllText(Path.Combine(changesDir, "change_aaa.jsonl"), string.Join('\n', new[]
        {
            """{"kind":"meta","follows":null}""",
            $$"""{"kind":"set","at":"2026-05-01T10:00:00Z","by":"tester","issueId":"{{id}}","property":"title","value":"early"}""",
            "",
        }));
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "commit1: change_aaa");

        // Second commit: change_bbb (sets title to "late")
        File.WriteAllText(Path.Combine(changesDir, "change_bbb.jsonl"), string.Join('\n', new[]
        {
            """{"kind":"meta","follows":null}""",
            $$"""{"kind":"set","at":"2026-05-01T11:00:00Z","by":"tester","issueId":"{{id}}","property":"title","value":"late"}""",
            "",
        }));
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "commit2: change_bbb");

        // Drop the active-change pointer so the next read doesn't try to use it.
        var pointerPath = Path.Combine(TempDir, ".fleece", ".active-change");
        if (File.Exists(pointerPath))
        {
            File.Delete(pointerPath);
        }

        var issues = await ReadIssuesAsync();
        issues.Single(i => i.Id == id).Title.Should().Be("late");
    }

    [Test]
    public async Task Same_commit_parallel_roots_tiebreak_by_guid_alphabetical()
    {
        // Seed: create an issue, project to clear change files, commit.
        (await RunCliAsync("create", "-t", "GUID order", "-y", "task", "-d", "x")).Should().Be(0);
        (await RunCliAsync("project")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "seed-projection");

        var seed = await ReadIssuesAsync();
        var id = seed.Single().Id;

        // Manually plant two change files in the same commit. Both have follows=null,
        // so GUID alphabetical order ("aaa" < "bbb") means aaa replays first, bbb wins.
        var changesDir = ChangesDir;
        Directory.CreateDirectory(changesDir);

        File.WriteAllText(Path.Combine(changesDir, "change_aaa.jsonl"), string.Join('\n', new[]
        {
            """{"kind":"meta","follows":null}""",
            $$"""{"kind":"set","at":"2026-05-01T10:00:00Z","by":"tester","issueId":"{{id}}","property":"title","value":"from-aaa"}""",
            "",
        }));
        File.WriteAllText(Path.Combine(changesDir, "change_bbb.jsonl"), string.Join('\n', new[]
        {
            """{"kind":"meta","follows":null}""",
            $$"""{"kind":"set","at":"2026-05-01T11:00:00Z","by":"tester","issueId":"{{id}}","property":"title","value":"from-bbb"}""",
            "",
        }));

        RunGit("add", ".fleece");
        RunGit("commit", "-m", "same-commit: both change files");

        var pointerPath = Path.Combine(TempDir, ".fleece", ".active-change");
        if (File.Exists(pointerPath))
        {
            File.Delete(pointerPath);
        }

        var issues = await ReadIssuesAsync();
        issues.Single(i => i.Id == id).Title.Should().Be("from-bbb");
    }

    [Test]
    public async Task Migrate_then_create_then_project_round_trip_stays_consistent()
    {
        // Plant legacy hashed files manually, run migrate, then exercise the new path.
        var fleeceDir = Path.Combine(TempDir, ".fleece");
        Directory.CreateDirectory(fleeceDir);
        await File.WriteAllTextAsync(
            Path.Combine(fleeceDir, "issues_aaa.jsonl"),
            """{"id":"old1","title":"Legacy","titleLastUpdate":"2026-04-01T10:00:00Z","status":"open","statusLastUpdate":"2026-04-01T10:00:00Z","type":"task","typeLastUpdate":"2026-04-01T10:00:00Z","createdAt":"2026-03-01T10:00:00Z","lastUpdate":"2026-04-01T10:00:00Z"}""" + "\n",
            Encoding.UTF8);

        (await RunCliAsync("migrate")).Should().Be(0);

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

    // --- Per-commit rotation -------------------------------------------------

    [Test]
    public async Task Multiple_edits_within_one_commit_accumulate_into_single_file()
    {
        (await RunCliAsync("create", "-t", "Acc", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: seed");

        var seed = await ReadIssuesAsync();
        var id = seed.Single().Id;

        // Three edits, no commit between them. Per-commit rotation should leave
        // them in the same change file because none has been committed since the
        // previous one was created.
        var before = Directory.GetFiles(ChangesDir).Length;
        (await RunCliAsync("edit", id, "-t", "v1")).Should().Be(0);
        (await RunCliAsync("edit", id, "-t", "v2")).Should().Be(0);
        (await RunCliAsync("edit", id, "-t", "v3")).Should().Be(0);
        var after = Directory.GetFiles(ChangesDir).Length;

        (after - before).Should().Be(1, because: "all three edits should land in the same new change file");
    }

    [Test]
    public async Task First_edit_after_commit_rotates_into_new_file_with_follows_to_previous()
    {
        (await RunCliAsync("create", "-t", "Seed", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: seed");

        var seed = await ReadIssuesAsync();
        var id = seed.Single().Id;

        var firstChangeFiles = Directory.GetFiles(ChangesDir).OrderBy(f => f).ToArray();
        firstChangeFiles.Length.Should().Be(1);
        var firstGuid = Path.GetFileNameWithoutExtension(firstChangeFiles[0])["change_".Length..];

        // First edit *after* the commit → previous file is committed at HEAD → rotate.
        (await RunCliAsync("edit", id, "-t", "Edited")).Should().Be(0);

        var allChangeFiles = Directory.GetFiles(ChangesDir).OrderBy(f => f).ToArray();
        allChangeFiles.Length.Should().Be(2);
        var newFile = allChangeFiles.Single(f => f != firstChangeFiles[0]);

        var firstLine = (await File.ReadAllLinesAsync(newFile))[0];
        firstLine.Should().Contain($"\"follows\":\"{firstGuid}\"");
    }

    // --- Bug 1 (fixed): merge conflicts on shared change files ---------------

    [Test]
    public async Task Same_worktree_two_branches_from_main_edits_conflict_on_merge()
    {
        // Repro of Bug 1 from the per-commit-change-rotation change.
        // Pre-fix: branching from main twice and editing on each branch reused
        // the same change file GUID → merging caused a content conflict on the
        // change file. Post-fix: per-commit rotation seals the main file on commit,
        // each branch's first edit rotates to a fresh GUID — no conflict.

        (await RunCliAsync("create", "-t", "Seed", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: seed");

        var seed = await ReadIssuesAsync();
        var id = seed.Single().Id;

        // feature/a
        RunGit("checkout", "-b", "feature/a");
        (await RunCliAsync("edit", id, "-t", "From A")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature/a: edit");

        // back to main
        RunGit("checkout", "main");

        // feature/b
        RunGit("checkout", "-b", "feature/b");
        (await RunCliAsync("edit", id, "-s", "Complete")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature/b: edit");

        // Merge feature/a into main
        RunGit("checkout", "main");
        RunGit("merge", "feature/a", "--no-edit");

        // Merge feature/b into main — must succeed without conflicts.
        Action act = () => RunGit("merge", "feature/b", "--no-edit");
        act.Should().NotThrow("per-commit rotation prevents reuse of the same change file across branches");

        var merged = (await ReadIssuesAsync()).Single();
        merged.Title.Should().Be("From A");
        merged.Status.Should().Be(IssueStatus.Complete);
    }

    // --- Marker generation ---------------------------------------------------

    [Test]
    public async Task Merge_marker_written_by_pre_merge_commit_hook_includes_both_leaves()
    {
        // Simulates the pre-merge-commit hook by calling `fleece link --merge` between
        // `git merge --no-commit` (which leaves MERGE_HEAD in place) and the final commit.
        (await RunCliAsync("create", "-t", "Seed", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: seed");
        var id = (await ReadIssuesAsync()).Single().Id;

        RunGit("checkout", "-b", "feature/a");
        (await RunCliAsync("edit", id, "-t", "A")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature/a");

        RunGit("checkout", "main");
        RunGit("checkout", "-b", "feature/b");
        (await RunCliAsync("edit", id, "-s", "Complete")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature/b");

        RunGit("checkout", "main");
        RunGit("merge", "feature/a", "--no-edit");

        // Two-way clean merge of feature/b: --no-commit emulates pre-merge-commit timing.
        RunGit("merge", "feature/b", "--no-ff", "--no-commit");
        (await RunCliAsync("link", "--merge")).Should().Be(0);
        RunGit("commit", "--no-edit");

        var changeFiles = Directory.GetFiles(ChangesDir).OrderBy(f => f).ToArray();
        var markers = changeFiles.Select(f => (path: f, body: File.ReadAllText(f)))
            .Where(t => t.body.Contains("\"follows\":["))
            .ToList();
        markers.Should().NotBeEmpty("link --merge should have written a multi-parent marker file");
    }

    [Test]
    public async Task Conflict_merge_pre_commit_hook_also_writes_marker()
    {
        // Two branches edit the same property → git merge produces a conflict on the
        // change file would happen pre-fix; with per-commit rotation the change files
        // are different and git merges them cleanly via "both modified" of different
        // files… so to force a conflict we make both branches edit an UNRELATED tracked
        // file. The marker is what we're verifying lands during the pre-commit path.
        (await RunCliAsync("create", "-t", "Seed", "-y", "task", "-d", "x")).Should().Be(0);
        File.WriteAllText(Path.Combine(TempDir, "shared.txt"), "base\n");
        RunGit("add", ".fleece", "shared.txt");
        RunGit("commit", "-m", "main: seed");
        var id = (await ReadIssuesAsync()).Single().Id;

        RunGit("checkout", "-b", "feature/a");
        (await RunCliAsync("edit", id, "-t", "A")).Should().Be(0);
        File.WriteAllText(Path.Combine(TempDir, "shared.txt"), "A wins\n");
        RunGit("add", ".fleece", "shared.txt");
        RunGit("commit", "-m", "feature/a");

        RunGit("checkout", "main");
        RunGit("checkout", "-b", "feature/b");
        (await RunCliAsync("edit", id, "-s", "Complete")).Should().Be(0);
        File.WriteAllText(Path.Combine(TempDir, "shared.txt"), "B wins\n");
        RunGit("add", ".fleece", "shared.txt");
        RunGit("commit", "-m", "feature/b");

        RunGit("checkout", "main");
        RunGit("merge", "feature/a", "--no-edit");

        // Merge feature/b — conflicts on shared.txt.
        Action mergeAct = () => RunGit("merge", "feature/b", "--no-edit");
        mergeAct.Should().Throw<InvalidOperationException>("shared.txt conflicts on both branches");

        // Resolve the conflict manually.
        File.WriteAllText(Path.Combine(TempDir, "shared.txt"), "resolved\n");
        RunGit("add", "shared.txt");

        // Simulate the pre-commit hook: write the marker before committing.
        (await RunCliAsync("link", "--merge")).Should().Be(0);
        RunGit("commit", "--no-edit");

        var markers = Directory.GetFiles(ChangesDir)
            .Where(f => File.ReadAllText(f).Contains("\"follows\":["))
            .ToList();
        markers.Should().NotBeEmpty("link --merge during pre-commit should write a multi-parent marker");
    }

    [Test]
    public async Task Interactive_rebase_drop_commit_leaves_dangling_followers_treated_as_roots()
    {
        // Build feature with two commits, each producing a change file chained off
        // the previous. Drop the first commit via rebase --rebase-merges --onto, then
        // verify replay tolerates the dangling follows pointer in the surviving file.
        (await RunCliAsync("create", "-t", "Seed", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: seed");
        var id = (await ReadIssuesAsync()).Single().Id;
        var mainSha = GitOutput("rev-parse", "HEAD").Trim();

        RunGit("checkout", "-b", "feature/d");
        (await RunCliAsync("edit", id, "-t", "v1")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature/d: v1 (will be dropped)");
        var dropCommit = GitOutput("rev-parse", "HEAD").Trim();

        (await RunCliAsync("edit", id, "-t", "v2")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature/d: v2");

        // Drop the v1 commit by rebasing v2 directly onto main.
        // Using `rebase --onto main <dropCommit> HEAD` keeps commits *after* dropCommit only.
        RunGit("rebase", "--onto", mainSha, dropCommit, "HEAD");

        // Replay must complete; the surviving change file's `follows` (pointing at the
        // dropped commit's file) is dangling — silently treated as root.
        var issues = await ReadIssuesAsync();
        issues.Should().ContainSingle(i => i.Id == id);
    }

    [Test]
    public async Task Fast_forward_merge_writes_no_marker()
    {
        (await RunCliAsync("create", "-t", "Seed", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: seed");
        var id = (await ReadIssuesAsync()).Single().Id;

        RunGit("checkout", "-b", "feature/ff");
        (await RunCliAsync("edit", id, "-t", "FF")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature/ff");

        var beforeMerge = Directory.GetFiles(ChangesDir).Length;

        RunGit("checkout", "main");
        RunGit("merge", "feature/ff", "--ff-only");

        var afterMerge = Directory.GetFiles(ChangesDir).Length;
        // Fast-forward leaves no new commit, hence no marker file. We didn't
        // invoke link, so the count should be exactly what feature/ff added.
        afterMerge.Should().Be(beforeMerge);
    }

    // --- Bug 2 (fixed): squash of integration branch preserves order ---------

    [Test]
    public async Task Squash_of_integration_branch_with_parallel_chains_loses_order()
    {
        // Repro of Bug 2 from the per-commit-change-rotation change, in its
        // post-fix form (asserts the corrected outcome): with merge markers
        // written during the regular merges into develop, the squash from
        // develop into main preserves the intended ordering.

        var seedDate = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var aDate = new DateTimeOffset(2026, 5, 1, 11, 0, 0, TimeSpan.Zero);
        var bDate = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var mergeADate = new DateTimeOffset(2026, 5, 1, 13, 0, 0, TimeSpan.Zero);
        var mergeBDate = new DateTimeOffset(2026, 5, 1, 14, 0, 0, TimeSpan.Zero);

        (await RunCliAsync("create", "-t", "Seed", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGitCommit("main: seed", seedDate);
        var id = (await ReadIssuesAsync()).Single().Id;

        RunGit("checkout", "-b", "develop");

        // feature/a from develop
        RunGit("checkout", "-b", "feature/a");
        (await RunCliAsync("edit", id, "-t", "A")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGitCommit("feature/a: title=A", aDate);

        // feature/b from develop
        RunGit("checkout", "develop");
        RunGit("checkout", "-b", "feature/b");
        (await RunCliAsync("edit", id, "-t", "B")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGitCommit("feature/b: title=B", bDate);

        // Merge feature/a into develop, writing a marker via --no-commit + link.
        RunGit("checkout", "develop");
        RunGit("merge", "feature/a", "--no-ff", "--no-commit");
        (await RunCliAsync("link", "--merge")).Should().Be(0);
        RunGitCommit("merge: feature/a", mergeADate);

        // Merge feature/b into develop with a marker.
        RunGit("merge", "feature/b", "--no-ff", "--no-commit");
        (await RunCliAsync("link", "--merge")).Should().Be(0);
        RunGitCommit("merge: feature/b", mergeBDate);

        // Squash develop into main → all change files (including markers) ride along.
        RunGit("checkout", "main");
        RunGit("merge", "--squash", "develop");
        RunGit("commit", "-m", "squash: develop");

        var post = (await ReadIssuesAsync()).Single();
        post.Title.Should().Be("B", because: "feature/b was merged into develop after feature/a, and the markers preserve that order through the squash");
    }

    [Test]
    public async Task Squash_merge_of_integration_branch_with_markers_preserves_order()
    {
        // Same scenario as Squash_of_integration_branch_with_parallel_chains_loses_order
        // (intentional alias spelled per task 7.6); kept distinct to make the wiring of
        // markers through a squash a first-class test surface.
        var aDate = new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero);
        var bDate = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var mADate = new DateTimeOffset(2026, 6, 1, 13, 0, 0, TimeSpan.Zero);
        var mBDate = new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.Zero);

        (await RunCliAsync("create", "-t", "PreservesOrder", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: seed");
        var id = (await ReadIssuesAsync()).Single().Id;

        RunGit("checkout", "-b", "develop");

        RunGit("checkout", "-b", "feature/a");
        (await RunCliAsync("edit", id, "-t", "A")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGitCommit("feature/a", aDate);

        RunGit("checkout", "develop");
        RunGit("checkout", "-b", "feature/b");
        (await RunCliAsync("edit", id, "-t", "B")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGitCommit("feature/b", bDate);

        RunGit("checkout", "develop");
        RunGit("merge", "feature/a", "--no-ff", "--no-commit");
        (await RunCliAsync("link", "--merge")).Should().Be(0);
        RunGitCommit("merge a", mADate);

        RunGit("merge", "feature/b", "--no-ff", "--no-commit");
        (await RunCliAsync("link", "--merge")).Should().Be(0);
        RunGitCommit("merge b", mBDate);

        // Capture the marker files so we can prove they survive the squash.
        var markerNames = Directory.GetFiles(ChangesDir)
            .Where(f => File.ReadAllText(f).Contains("\"follows\":["))
            .Select(Path.GetFileName)
            .ToList();
        markerNames.Should().NotBeEmpty();

        RunGit("checkout", "main");
        RunGit("merge", "--squash", "develop");
        RunGit("commit", "-m", "squash: develop");

        var postNames = Directory.GetFiles(ChangesDir).Select(Path.GetFileName).ToList();
        foreach (var name in markerNames)
        {
            postNames.Should().Contain(name, because: "markers must survive the squash");
        }

        (await ReadIssuesAsync()).Single().Title.Should().Be("B");
    }

    // --- Rebase / cherry-pick ------------------------------------------------

    [Test]
    public async Task Rebase_feature_onto_main_with_stale_follows_replays_via_commit_ordinal()
    {
        var seedDate = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var f1Date = new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero);
        var f2Date = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var mainAdvanceDate = new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero);

        (await RunCliAsync("create", "-t", "Rebase", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGitCommit("main: seed", seedDate);
        var id = (await ReadIssuesAsync()).Single().Id;

        RunGit("checkout", "-b", "feature/r");
        (await RunCliAsync("edit", id, "-t", "v1")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGitCommit("feature/r: v1", f1Date);

        (await RunCliAsync("edit", id, "-t", "v2")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGitCommit("feature/r: v2", f2Date);

        // Advance main with an unrelated change.
        RunGit("checkout", "main");
        (await RunCliAsync("create", "-t", "Other", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGitCommit("main: other", mainAdvanceDate);

        // Rebase feature onto main.
        RunGit("checkout", "feature/r");
        RunGit("rebase", "main");

        // Replay must still produce the correct final title=v2 even though follows
        // pointers across the rebased commits are stale.
        (await ReadIssuesAsync()).Single(i => i.Id == id).Title.Should().Be("v2");
    }

    [Test]
    public async Task Cherry_pick_commit_with_dangling_follows_treats_file_as_root()
    {
        // The cherry-picked feature commit brings its edit change file (`follows` points
        // at the seed file, which is absent on the orphan branch). Per the spec, dangling
        // follows entries are silently dropped and the file becomes a DAG root — replay
        // must complete without throwing.
        (await RunCliAsync("create", "-t", "Source", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: seed");
        var id = (await ReadIssuesAsync()).Single().Id;

        RunGit("checkout", "-b", "feature/c");
        (await RunCliAsync("edit", id, "-t", "Cherry-picked")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature/c: edit");
        var cherrySha = GitOutput("rev-parse", "HEAD").Trim();

        // Build a brand-new orphan branch with no shared history.
        RunGit("checkout", "--orphan", "orphan");
        RunGit("rm", "-rf", ".");

        // Seed orphan with a minimal commit so HEAD exists.
        File.WriteAllText(Path.Combine(TempDir, "readme.md"), "orphan\n");
        RunGit("add", "readme.md");
        RunGit("commit", "-m", "orphan: seed");

        RunGit("cherry-pick", cherrySha);

        // Replay must succeed without throwing; the dangling follows is silently dropped
        // and the orphan file becomes a DAG root. The issue itself won't materialise
        // because the original Create event is on the (now-absent) seed file — what we
        // assert is that replay produces a well-formed (possibly empty) state.
        var issues = await ReadIssuesAsync();
        issues.Should().NotBeNull();
    }

    // --- Round-trip ----------------------------------------------------------

    [Test]
    public async Task Project_after_merge_marker_collapses_marker_into_snapshot()
    {
        (await RunCliAsync("create", "-t", "Seed", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: seed");
        var id = (await ReadIssuesAsync()).Single().Id;

        RunGit("checkout", "-b", "feature/p");
        (await RunCliAsync("edit", id, "-t", "Final")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature/p");

        RunGit("checkout", "main");
        RunGit("merge", "feature/p", "--no-ff", "--no-commit");
        (await RunCliAsync("link", "--merge")).Should().Be(0);
        RunGit("commit", "--no-edit");

        // Project should collapse all change files (including the marker) into the snapshot.
        (await RunCliAsync("project")).Should().Be(0);

        Directory.GetFiles(ChangesDir).Should().BeEmpty();
        File.Exists(SnapshotPath).Should().BeTrue();
        (await ReadIssuesAsync()).Single().Title.Should().Be("Final");
    }

    [Test]
    public async Task Migrate_then_create_then_project_round_trip_stays_consistent_with_merge()
    {
        var fleeceDir = Path.Combine(TempDir, ".fleece");
        Directory.CreateDirectory(fleeceDir);
        await File.WriteAllTextAsync(
            Path.Combine(fleeceDir, "issues_aaa.jsonl"),
            """{"id":"old1","title":"Legacy","titleLastUpdate":"2026-04-01T10:00:00Z","status":"open","statusLastUpdate":"2026-04-01T10:00:00Z","type":"task","typeLastUpdate":"2026-04-01T10:00:00Z","createdAt":"2026-03-01T10:00:00Z","lastUpdate":"2026-04-01T10:00:00Z"}""" + "\n",
            System.Text.Encoding.UTF8);

        (await RunCliAsync("migrate")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "migrate");

        (await RunCliAsync("create", "-t", "Fresh", "-y", "task", "-d", "x")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "main: fresh");

        var fresh = (await ReadIssuesAsync()).Single(i => i.Title == "Fresh").Id;

        RunGit("checkout", "-b", "feature/edit");
        (await RunCliAsync("edit", fresh, "-t", "Edited")).Should().Be(0);
        RunGit("add", ".fleece");
        RunGit("commit", "-m", "feature/edit");

        RunGit("checkout", "main");
        RunGit("merge", "feature/edit", "--no-ff", "--no-commit");
        (await RunCliAsync("link", "--merge")).Should().Be(0);
        RunGit("commit", "--no-edit");

        (await RunCliAsync("project")).Should().Be(0);

        var after = await ReadIssuesAsync();
        after.Should().HaveCount(2);
        after.Single(i => i.Id == fresh).Title.Should().Be("Edited");
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
        var gitContext = new GitEventContext(new GitService(TempDir));

        var initial = await snapshot.LoadSnapshotAsync();
        var changeFiles = await eventStore.GetAllChangeFilePathsAsync();
        if (changeFiles.Count == 0)
        {
            return initial.Values.ToList();
        }
        var state = await replay.ReplayAsync(initial, changeFiles, gitContext);
        return state.Values.ToList();
    }
}
