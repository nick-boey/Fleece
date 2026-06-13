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
        // `fleece prime v4-migration` warning, so migration reports nothing to do.
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
}
