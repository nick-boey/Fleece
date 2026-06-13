using System.Text;
using Fleece.Core.EventSourcing.Services;
using Fleece.Core.EventSourcing.Services.Legacy;
using Fleece.Core.Models;
using FluentAssertions;
using Testably.Abstractions.Testing;

namespace Fleece.Core.Tests.EventSourcing;

[TestFixture]
public sealed class MigrationServiceTests
{
    private MockFileSystem _fs = null!;
    private string _basePath = null!;
    private MigrationService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _fs = new MockFileSystem();
        _basePath = "/repo";
        _fs.Directory.CreateDirectory(_basePath);
        _sut = new MigrationService(_basePath, _fs);
    }

    private string FleeceDir => Path.Combine(_basePath, ".fleece");
    private string IssuesDir => Path.Combine(FleeceDir, "issues");

    private void WriteLegacyIssuesFile(string hash, params string[] jsonLines)
    {
        _fs.Directory.CreateDirectory(FleeceDir);
        var content = string.Join('\n', jsonLines) + "\n";
        _fs.File.WriteAllText(Path.Combine(FleeceDir, $"issues_{hash}.jsonl"), content, Encoding.UTF8);
    }

    private void WriteLegacyTombstonesFile(string hash, params string[] jsonLines)
    {
        _fs.Directory.CreateDirectory(FleeceDir);
        var content = string.Join('\n', jsonLines) + "\n";
        _fs.File.WriteAllText(Path.Combine(FleeceDir, $"tombstones_{hash}.jsonl"), content, Encoding.UTF8);
    }

    private static string LegacyIssueJson(string id, string title,
        string status = "open", string lastUpdate = "2026-04-01T10:00:00Z") =>
        $$"""
        {"id":"{{id}}","title":"{{title}}","titleLastUpdate":"{{lastUpdate}}","status":"{{status}}","statusLastUpdate":"{{lastUpdate}}","type":"task","typeLastUpdate":"{{lastUpdate}}","createdAt":"2026-03-01T10:00:00Z","lastUpdate":"{{lastUpdate}}"}
        """;

    /// <summary>Replays the v4 per-issue logs the migration produced.</summary>
    private async Task<IReadOnlyDictionary<string, Issue>> LoadMigratedIssuesAsync()
    {
        var store = new EventStore(_basePath, _fs);
        var replay = new ReplayEngine(store);
        var paths = await store.GetAllIssueLogPathsAsync();
        return await replay.ReplayAsync(paths);
    }

    [Test]
    public async Task IsMigrationNeeded_returns_false_when_only_a_legacy_snapshot_is_present()
    {
        // A durable `.fleece/issues.jsonl` snapshot is NOT auto-migrated; it is handled by the
        // v4-migration warning, so migration reports nothing to do.
        _fs.Directory.CreateDirectory(FleeceDir);
        _fs.File.WriteAllText(Path.Combine(FleeceDir, "issues.jsonl"), "");

        (await _sut.IsMigrationNeededAsync()).Should().BeFalse();
    }

    [Test]
    public async Task IsMigrationNeeded_returns_true_when_legacy_issue_files_exist()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "First"));
        (await _sut.IsMigrationNeededAsync()).Should().BeTrue();
    }

    [Test]
    public async Task Migrate_consolidates_two_files_into_per_issue_logs()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "First"), LegacyIssueJson("i2", "Second"));
        WriteLegacyIssuesFile("bbb", LegacyIssueJson("i3", "Third"));

        var result = await _sut.MigrateAsync("tester");

        result.WasMigrationNeeded.Should().BeTrue();
        result.LegacyIssueFilesConsumed.Should().Be(2);
        result.IssuesWritten.Should().Be(3);

        _fs.File.Exists(Path.Combine(IssuesDir, "i1.jsonl")).Should().BeTrue();
        _fs.File.Exists(Path.Combine(IssuesDir, "i2.jsonl")).Should().BeTrue();
        _fs.File.Exists(Path.Combine(IssuesDir, "i3.jsonl")).Should().BeTrue();
        _fs.File.Exists(Path.Combine(FleeceDir, "issues_aaa.jsonl")).Should().BeFalse();
        _fs.File.Exists(Path.Combine(FleeceDir, "issues_bbb.jsonl")).Should().BeFalse();

        var issues = await LoadMigratedIssuesAsync();
        issues.Keys.Should().BeEquivalentTo(["i1", "i2", "i3"]);
    }

    [Test]
    public async Task Migrate_does_not_write_legacy_snapshot_or_changes_directory()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "First"));

        await _sut.MigrateAsync();

        _fs.File.Exists(Path.Combine(FleeceDir, "issues.jsonl")).Should().BeFalse();
        _fs.Directory.Exists(Path.Combine(FleeceDir, "changes")).Should().BeFalse();
    }

    [Test]
    public async Task Migrate_reconciles_overlapping_issue_via_property_level_merge()
    {
        // Same id "shared" present in both files; bbb has a later titleLastUpdate.
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("shared", "Old Title", lastUpdate: "2026-04-01T10:00:00Z"));
        WriteLegacyIssuesFile("bbb", LegacyIssueJson("shared", "New Title", lastUpdate: "2026-04-15T10:00:00Z"));

        var result = await _sut.MigrateAsync();

        result.IssuesWritten.Should().Be(1);
        var issues = await LoadMigratedIssuesAsync();
        issues["shared"].Title.Should().Be("New Title");
    }

    [Test]
    public async Task Migrate_strips_per_property_timestamps_from_logs()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Hello"));

        await _sut.MigrateAsync();

        var log = await _fs.File.ReadAllTextAsync(Path.Combine(IssuesDir, "i1.jsonl"));
        log.Should().NotContain("titleLastUpdate");
        log.Should().NotContain("statusLastUpdate");
        log.Should().NotContain("typeLastUpdate");
        log.Should().NotContain("ModifiedBy");
    }

    [Test]
    public async Task Migrate_does_not_write_tombstone_sidecar()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Stays"));
        WriteLegacyTombstonesFile("aaa",
            """{"issueId":"t1","originalTitle":"Gone A","cleanedAt":"2026-04-01T10:00:00Z","cleanedBy":"alice"}""");

        var result = await _sut.MigrateAsync();

        result.TombstonesWritten.Should().Be(0);
        result.LegacyTombstoneFilesConsumed.Should().Be(1);
        _fs.File.Exists(Path.Combine(FleeceDir, "tombstones.jsonl")).Should().BeFalse();
        _fs.File.Exists(Path.Combine(FleeceDir, "tombstones_aaa.jsonl")).Should().BeFalse();
    }

    [Test]
    public async Task Migrate_adds_no_gitignore_entries()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Hello"));

        var result = await _sut.MigrateAsync();

        result.GitignoreEntriesAdded.Should().BeEmpty();
        _fs.File.Exists(Path.Combine(_basePath, ".gitignore")).Should().BeFalse();
    }

    [Test]
    public async Task Migrate_is_idempotent_on_already_migrated_repository()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Hello"));
        await _sut.MigrateAsync();

        var result = await _sut.MigrateAsync();
        result.WasMigrationNeeded.Should().BeFalse();
        result.IssuesWritten.Should().Be(0);
        result.GitignoreEntriesAdded.Should().BeEmpty();
    }

    [Test]
    public async Task Migrate_is_safe_when_only_tombstone_files_exist()
    {
        WriteLegacyTombstonesFile("aaa",
            """{"issueId":"t1","originalTitle":"Lone","cleanedAt":"2026-04-01T10:00:00Z","cleanedBy":"alice"}""");

        var result = await _sut.MigrateAsync();

        result.WasMigrationNeeded.Should().BeTrue();
        result.IssuesWritten.Should().Be(0);
        result.TombstonesWritten.Should().Be(0);
    }

    [Test]
    public async Task Migrate_folds_pre_3_0_0_LinkedPR_scalar_into_Tags_keyed_tag()
    {
        // Pre-3.0.0 issue: scalar LinkedPR set, no linked-pr keyed-tag in Tags,
        // and per-property LastUpdate timestamps zeroed (a fresh-from-old-fleece shape).
        WriteLegacyIssuesFile("aaa",
            """{"id":"i1","title":"Old","status":"open","type":"task","linkedPR":42,"createdAt":"2026-03-01T10:00:00Z","lastUpdate":"2026-03-01T10:00:00Z"}""");

        var result = await _sut.MigrateAsync();
        result.WasMigrationNeeded.Should().BeTrue();
        result.IssuesWritten.Should().Be(1);

        var log = await _fs.File.ReadAllTextAsync(Path.Combine(IssuesDir, "i1.jsonl"));
        log.Should().Contain($"{KeyedTag.LinkedPrKey}=42",
            "the LinkedPR scalar should be folded into Tags as a keyed-tag entry");
        log.Should().NotContain("\"linkedPR\":42",
            "the scalar LinkedPR field must be cleared after fold-in");
    }

    [Test]
    public async Task Migrate_maps_removed_idea_type_onto_task()
    {
        WriteLegacyIssuesFile("aaa",
            """{"id":"i1","title":"An idea","status":"open","type":"idea","createdAt":"2026-03-01T10:00:00Z","lastUpdate":"2026-03-01T10:00:00Z"}""");

        await _sut.MigrateAsync();

        var issues = await LoadMigratedIssuesAsync();
        issues["i1"].Type.Should().Be(IssueType.Task);
    }

    // ----- Layout B: legacy durable snapshot (`.fleece/issues.jsonl` + `.fleece/changes/`) -----

    private string ChangesDir => Path.Combine(FleeceDir, "changes");

    private void WriteDurableSnapshot(params string[] jsonLines)
    {
        _fs.Directory.CreateDirectory(FleeceDir);
        var content = string.Join('\n', jsonLines) + "\n";
        _fs.File.WriteAllText(Path.Combine(FleeceDir, "issues.jsonl"), content, Encoding.UTF8);
    }

    private void WriteChangeFile(string guid, params string[] jsonLines)
    {
        _fs.Directory.CreateDirectory(ChangesDir);
        var content = string.Join('\n', jsonLines) + "\n";
        _fs.File.WriteAllText(Path.Combine(ChangesDir, $"change_{guid}.jsonl"), content, Encoding.UTF8);
    }

    private static string LeanIssueJson(string id, string title, string status = "open") =>
        $$"""
        {"id":"{{id}}","title":"{{title}}","status":"{{status}}","type":"task","createdAt":"2026-03-01T10:00:00Z","lastUpdate":"2026-03-01T10:00:00Z"}
        """;

    private static string MetaLine(string? follows = null) =>
        follows is null
            ? """{"kind":"meta","follows":null}"""
            : $$"""{"kind":"meta","follows":"{{follows}}"}""";

    private static string SetTitleLine(string id, string title, string at = "2026-04-01T10:00:00Z") =>
        $$"""{"kind":"set","at":"{{at}}","issueId":"{{id}}","property":"title","value":"{{title}}"}""";

    [Test]
    public async Task Migrate_converts_durable_snapshot_only_into_per_issue_logs()
    {
        WriteDurableSnapshot(LeanIssueJson("d1", "Alpha"), LeanIssueJson("d2", "Beta"));

        var result = await _sut.MigrateAsync("tester");

        result.WasMigrationNeeded.Should().BeTrue();
        result.IssuesWritten.Should().Be(2);

        var issues = await LoadMigratedIssuesAsync();
        issues.Keys.Should().BeEquivalentTo(["d1", "d2"]);
        issues["d1"].Title.Should().Be("Alpha");
        issues["d2"].Title.Should().Be("Beta");

        // Source snapshot is consumed.
        _fs.File.Exists(Path.Combine(FleeceDir, "issues.jsonl")).Should().BeFalse();
    }

    [Test]
    public async Task Migrate_durable_snapshot_plus_change_file_applies_events()
    {
        WriteDurableSnapshot(LeanIssueJson("d1", "Old"));
        WriteChangeFile("aaa", MetaLine(), SetTitleLine("d1", "New"));

        await _sut.MigrateAsync("tester");

        var issues = await LoadMigratedIssuesAsync();
        issues["d1"].Title.Should().Be("New", "the change-file event must be replayed over the snapshot");

        _fs.File.Exists(Path.Combine(FleeceDir, "issues.jsonl")).Should().BeFalse();
        _fs.Directory.Exists(ChangesDir).Should().BeFalse();
    }

    [Test]
    public async Task Migrate_durable_replays_change_files_in_follows_order_not_file_order()
    {
        // Snapshot starts at "v0". Three change files chain A → B → C via `follows`, but their
        // GUIDs sort in the OPPOSITE order (ccc, bbb, aaa) so naive file-order replay would end
        // on "v1". Correct follows ordering must end on "v3".
        WriteDurableSnapshot(LeanIssueJson("d1", "v0"));
        WriteChangeFile("ccc", MetaLine(), SetTitleLine("d1", "v1"));          // A: root
        WriteChangeFile("bbb", MetaLine("ccc"), SetTitleLine("d1", "v2"));     // B: follows A
        WriteChangeFile("aaa", MetaLine("bbb"), SetTitleLine("d1", "v3"));     // C: follows B

        await _sut.MigrateAsync("tester");

        var issues = await LoadMigratedIssuesAsync();
        issues["d1"].Title.Should().Be("v3");
    }

    [Test]
    public async Task Migrate_durable_is_idempotent_second_run_is_a_clean_no_op()
    {
        WriteDurableSnapshot(LeanIssueJson("d1", "Once"));

        var first = await _sut.MigrateAsync("tester");
        first.WasMigrationNeeded.Should().BeTrue();

        var second = await _sut.MigrateAsync("tester");
        second.WasMigrationNeeded.Should().BeFalse();
        second.IssuesWritten.Should().Be(0);
    }

    [Test]
    public async Task IsMigrationNeeded_stays_false_for_durable_but_explicit_migrate_converts()
    {
        // The interceptor's auto-trigger must never fire on the durable layout...
        WriteDurableSnapshot(LeanIssueJson("d1", "Durable"));
        (await _sut.IsMigrationNeededAsync()).Should().BeFalse();

        // ...but the explicit `fleece migrate` command still converts it.
        await _sut.MigrateAsync("tester");
        var issues = await LoadMigratedIssuesAsync();
        issues.Should().ContainKey("d1");
    }

    [Test]
    public async Task Migrate_durable_remaps_removed_enum_members_in_snapshot()
    {
        // A genuine v3 snapshot can carry enum members v4 removed: type `idea`, status `archived`.
        WriteDurableSnapshot(
            """{"id":"d1","title":"Old idea","status":"archived","type":"idea","createdAt":"2026-03-01T10:00:00Z","lastUpdate":"2026-03-01T10:00:00Z"}""");

        await _sut.MigrateAsync("tester");

        var issues = await LoadMigratedIssuesAsync();
        issues["d1"].Type.Should().Be(IssueType.Task, "removed type `idea` maps to task");
        issues["d1"].Status.Should().Be(IssueStatus.Promoted, "removed status `archived` maps to promoted");
    }

    [Test]
    public async Task Migrate_durable_remaps_removed_enum_value_in_change_event()
    {
        // The latest state may live in a change-file event setting a now-removed status.
        WriteDurableSnapshot(LeanIssueJson("d1", "Active", status: "open"));
        WriteChangeFile("aaa", MetaLine(),
            """{"kind":"set","at":"2026-04-01T10:00:00Z","issueId":"d1","property":"status","value":"archived"}""");

        await _sut.MigrateAsync("tester");

        var issues = await LoadMigratedIssuesAsync();
        issues["d1"].Status.Should().Be(IssueStatus.Promoted,
            "a `set status=archived` change event must remap to promoted, not silently degrade");
    }

    [Test]
    public async Task Migrate_with_convertDurableLayout_false_leaves_the_durable_snapshot_intact()
    {
        // This is the interceptor's contract: even alongside hashed files (which it DOES
        // auto-migrate), the durable layout must never be auto-converted.
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("h1", "Hashed"));
        WriteDurableSnapshot(LeanIssueJson("d1", "Durable"));

        var result = await _sut.MigrateAsync("tester", convertDurableLayout: false);

        result.WasMigrationNeeded.Should().BeTrue("the hashed layout still migrates");

        var issues = await LoadMigratedIssuesAsync();
        issues.Should().ContainKey("h1");
        issues.Should().NotContainKey("d1", "the durable snapshot must not be auto-converted");

        // The durable sources are untouched.
        _fs.File.Exists(Path.Combine(FleeceDir, "issues.jsonl")).Should().BeTrue();
    }
}
