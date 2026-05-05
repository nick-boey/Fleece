using System.Text.Json;
using Fleece.Core.EventSourcing.Events;
using Fleece.Core.EventSourcing.Services;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;
using FluentAssertions;
using NUnit.Framework;
using Testably.Abstractions.Testing;
using Issue = Fleece.Core.Models.Issue;

namespace Fleece.Core.Tests.EventSourcing;

[TestFixture]
public sealed class ProjectionServiceTests
{
    private MockFileSystem _fs = null!;
    private string _basePath = null!;
    private SnapshotStore _snapshot = null!;
    private EventStore _eventStore = null!;
    private ReplayEngine _replay = null!;
    private ReplayCache _cache = null!;
    private NullEventGitContext _git = null!;
    private EventSourcedStorageService _storage = null!;
    private ProjectionService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _fs = new MockFileSystem();
        _basePath = "/repo";
        _fs.Directory.CreateDirectory(_basePath);
        _snapshot = new SnapshotStore(_basePath, _fs);
        var guidQueue = new Queue<string>(["g1", "g2", "g3", "g4"]);
        _eventStore = new EventStore(_basePath, _fs, () => guidQueue.Count > 0 ? guidQueue.Dequeue() : Guid.NewGuid().ToString("N"));
        _replay = new ReplayEngine(_eventStore);
        _cache = new ReplayCache(_basePath, _fs);
        _git = NullEventGitContext.Instance;
        _storage = new EventSourcedStorageService(_snapshot, _eventStore, _replay, _cache, _git);
        _sut = new ProjectionService(_storage);
    }

    private static CreateEvent Create(string id, string title, string status = "open") => new()
    {
        At = DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
        IssueId = id,
        Data = JsonDocument.Parse($$"""
            {"title":"{{title}}","status":"{{status}}","type":"task","createdAt":"2026-04-30T10:00:00Z","lastUpdate":"2026-04-30T10:00:00Z"}
            """).RootElement,
    };

    [Test]
    public async Task Project_writes_replayed_state_into_snapshot_and_deletes_change_files()
    {
        await _storage.AppendEventsAsync([Create("i1", "Alpha"), Create("i2", "Beta")]);

        var result = await _sut.ProjectAsync(DateTimeOffset.Parse("2026-05-01T00:00:00Z"), "tester");

        result.IssueCount.Should().Be(2);
        result.ChangeFilesCompacted.Should().Be(1);
        result.AutoCleanedTombstones.Should().BeEmpty();

        var changeFiles = await _eventStore.GetAllChangeFilePathsAsync();
        changeFiles.Should().BeEmpty();

        var snapshot = await _snapshot.LoadSnapshotAsync();
        snapshot.Should().HaveCount(2);
        snapshot["i1"].Title.Should().Be("Alpha");
        snapshot["i2"].Title.Should().Be("Beta");
    }

    [Test]
    public async Task Project_is_idempotent_on_repeat_run()
    {
        await _storage.AppendEventsAsync([Create("i1", "Alpha")]);
        await _sut.ProjectAsync(DateTimeOffset.Parse("2026-05-01T00:00:00Z"), "tester");

        var snapshotBefore = await _fs.File.ReadAllTextAsync(_snapshot.SnapshotPath);
        var tombstonesExistedBefore = _fs.File.Exists(_snapshot.TombstonesPath);
        var tombstonesBefore = tombstonesExistedBefore
            ? await _fs.File.ReadAllTextAsync(_snapshot.TombstonesPath)
            : string.Empty;

        var second = await _sut.ProjectAsync(DateTimeOffset.Parse("2026-05-01T00:00:00Z"), "tester");

        second.ChangeFilesCompacted.Should().Be(0);
        second.AutoCleanedTombstones.Should().BeEmpty();
        var snapshotAfter = await _fs.File.ReadAllTextAsync(_snapshot.SnapshotPath);
        snapshotAfter.Should().Be(snapshotBefore);
        var tombstonesAfter = _fs.File.Exists(_snapshot.TombstonesPath)
            ? await _fs.File.ReadAllTextAsync(_snapshot.TombstonesPath)
            : string.Empty;
        tombstonesAfter.Should().Be(tombstonesBefore);
    }

    [Test]
    public async Task Project_auto_cleans_soft_deleted_issues_older_than_30_days()
    {
        var deletedAt = DateTimeOffset.Parse("2026-03-15T10:00:00Z");
        await _snapshot.WriteSnapshotAsync(new Dictionary<string, Issue>
        {
            ["old"] = new()
            {
                Id = "old",
                Title = "Stale",
                Status = IssueStatus.Deleted,
                Type = IssueType.Task,
                CreatedAt = deletedAt,
                LastUpdate = deletedAt,
            }
        });

        var now = DateTimeOffset.Parse("2026-05-01T00:00:00Z");
        var result = await _sut.ProjectAsync(now, "auto-cleaner");

        result.AutoCleanedTombstones.Should().HaveCount(1);
        result.AutoCleanedTombstones[0].IssueId.Should().Be("old");
        result.AutoCleanedTombstones[0].OriginalTitle.Should().Be("Stale");
        result.AutoCleanedTombstones[0].CleanedAt.Should().Be(now);
        result.AutoCleanedTombstones[0].CleanedBy.Should().Be("auto-cleaner");

        var snapshot = await _snapshot.LoadSnapshotAsync();
        snapshot.Should().NotContainKey("old");

        var tombstones = await _snapshot.LoadTombstonesAsync();
        tombstones.Should().ContainSingle(t => t.IssueId == "old");
    }

    [Test]
    public async Task Project_does_not_clean_recently_deleted_issues()
    {
        var deletedAt = DateTimeOffset.Parse("2026-04-15T10:00:00Z");
        await _snapshot.WriteSnapshotAsync(new Dictionary<string, Issue>
        {
            ["recent"] = new()
            {
                Id = "recent",
                Title = "Fresh",
                Status = IssueStatus.Deleted,
                Type = IssueType.Task,
                CreatedAt = deletedAt,
                LastUpdate = deletedAt,
            }
        });

        var now = DateTimeOffset.Parse("2026-05-01T00:00:00Z");
        var result = await _sut.ProjectAsync(now, "auto-cleaner");

        result.AutoCleanedTombstones.Should().BeEmpty();
        var snapshot = await _snapshot.LoadSnapshotAsync();
        snapshot.Should().ContainKey("recent");
    }

    [Test]
    public async Task Project_preserves_existing_tombstones_when_cleaning_more()
    {
        await _snapshot.WriteTombstonesAsync(
        [
            new Tombstone
            {
                IssueId = "earlier",
                OriginalTitle = "Earlier",
                CleanedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                CleanedBy = "previous-run",
            }
        ]);

        var deletedAt = DateTimeOffset.Parse("2026-03-15T10:00:00Z");
        await _snapshot.WriteSnapshotAsync(new Dictionary<string, Issue>
        {
            ["old"] = new()
            {
                Id = "old",
                Title = "Old",
                Status = IssueStatus.Deleted,
                Type = IssueType.Task,
                CreatedAt = deletedAt,
                LastUpdate = deletedAt,
            }
        });

        var now = DateTimeOffset.Parse("2026-05-01T00:00:00Z");
        await _sut.ProjectAsync(now, "auto-cleaner");

        var tombstones = await _snapshot.LoadTombstonesAsync();
        tombstones.Select(t => t.IssueId).Should().BeEquivalentTo(["earlier", "old"]);
    }
}
