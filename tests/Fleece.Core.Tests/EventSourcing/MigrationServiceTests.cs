using System.Text;
using Fleece.Core.EventSourcing.Services.Legacy;
using Fleece.Core.Models;
using FluentAssertions;
using NUnit.Framework;
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

    [Test]
    public async Task IsMigrationNeeded_returns_false_on_already_migrated_repo()
    {
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
    public async Task Migrate_consolidates_two_files_into_single_snapshot()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "First"), LegacyIssueJson("i2", "Second"));
        WriteLegacyIssuesFile("bbb", LegacyIssueJson("i3", "Third"));

        var result = await _sut.MigrateAsync("tester");

        result.WasMigrationNeeded.Should().BeTrue();
        result.LegacyIssueFilesConsumed.Should().Be(2);
        result.IssuesWritten.Should().Be(3);
        _fs.File.Exists(Path.Combine(FleeceDir, "issues.jsonl")).Should().BeTrue();
        _fs.File.Exists(Path.Combine(FleeceDir, "issues_aaa.jsonl")).Should().BeFalse();
        _fs.File.Exists(Path.Combine(FleeceDir, "issues_bbb.jsonl")).Should().BeFalse();
    }

    [Test]
    public async Task Migrate_reconciles_overlapping_issue_via_property_level_merge()
    {
        // Same id "shared" present in both files; bbb has a later titleLastUpdate.
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("shared", "Old Title", lastUpdate: "2026-04-01T10:00:00Z"));
        WriteLegacyIssuesFile("bbb", LegacyIssueJson("shared", "New Title", lastUpdate: "2026-04-15T10:00:00Z"));

        var result = await _sut.MigrateAsync();

        result.IssuesWritten.Should().Be(1);
        var snapshotContent = await _fs.File.ReadAllTextAsync(Path.Combine(FleeceDir, "issues.jsonl"));
        snapshotContent.Should().Contain("\"title\":\"New Title\"");
    }

    [Test]
    public async Task Migrate_strips_per_property_timestamps_from_snapshot()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Hello"));

        await _sut.MigrateAsync();

        var snapshot = await _fs.File.ReadAllTextAsync(Path.Combine(FleeceDir, "issues.jsonl"));
        snapshot.Should().NotContain("titleLastUpdate");
        snapshot.Should().NotContain("statusLastUpdate");
        snapshot.Should().NotContain("typeLastUpdate");
        snapshot.Should().NotContain("ModifiedBy");
    }

    [Test]
    public async Task Migrate_unions_tombstones_from_multiple_files()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Stays"));
        WriteLegacyTombstonesFile("aaa",
            """{"issueId":"t1","originalTitle":"Gone A","cleanedAt":"2026-04-01T10:00:00Z","cleanedBy":"alice"}""");
        WriteLegacyTombstonesFile("bbb",
            """{"issueId":"t2","originalTitle":"Gone B","cleanedAt":"2026-04-02T10:00:00Z","cleanedBy":"bob"}""");

        var result = await _sut.MigrateAsync();

        result.TombstonesWritten.Should().Be(2);
        var content = await _fs.File.ReadAllTextAsync(Path.Combine(FleeceDir, "tombstones.jsonl"));
        content.Should().Contain("t1").And.Contain("t2");
        _fs.File.Exists(Path.Combine(FleeceDir, "tombstones_aaa.jsonl")).Should().BeFalse();
        _fs.File.Exists(Path.Combine(FleeceDir, "tombstones_bbb.jsonl")).Should().BeFalse();
    }

    [Test]
    public async Task Migrate_creates_changes_directory()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Hello"));

        await _sut.MigrateAsync();

        _fs.Directory.Exists(Path.Combine(FleeceDir, "changes")).Should().BeTrue();
    }

    [Test]
    public async Task Migrate_adds_gitignore_entries()
    {
        WriteLegacyIssuesFile("aaa", LegacyIssueJson("i1", "Hello"));

        var result = await _sut.MigrateAsync();

        result.GitignoreEntriesAdded.Should().BeEquivalentTo(
            [".fleece/.active-change", ".fleece/.replay-cache"]);
        var gitignore = await _fs.File.ReadAllTextAsync(Path.Combine(_basePath, ".gitignore"));
        gitignore.Should().Contain(".fleece/.active-change");
        gitignore.Should().Contain(".fleece/.replay-cache");
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
        result.TombstonesWritten.Should().Be(1);
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

        var snapshot = await _fs.File.ReadAllTextAsync(Path.Combine(FleeceDir, "issues.jsonl"));
        snapshot.Should().Contain($"{KeyedTag.LinkedPrKey}=42",
            "the LinkedPR scalar should be folded into Tags as a keyed-tag entry");
        snapshot.Should().NotContain("\"linkedPR\":42",
            "the scalar LinkedPR field must be cleared after fold-in");
    }
}
