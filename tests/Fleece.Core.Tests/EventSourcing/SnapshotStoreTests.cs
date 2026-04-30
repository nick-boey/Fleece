using Fleece.Core.EventSourcing.Services;
using Fleece.Core.Models;
using FluentAssertions;
using NUnit.Framework;
using Testably.Abstractions.Testing;
using Issue = Fleece.Core.EventSourcing.Issue;

namespace Fleece.Core.Tests.EventSourcing;

[TestFixture]
public sealed class SnapshotStoreTests
{
    private MockFileSystem _fs = null!;
    private SnapshotStore _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _fs = new MockFileSystem();
        _fs.Directory.CreateDirectory("/repo");
        _sut = new SnapshotStore("/repo", _fs);
    }

    private static Issue Make(string id, string title) => new()
    {
        Id = id,
        Title = title,
        Status = IssueStatus.Open,
        Type = IssueType.Task,
        CreatedAt = DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
        LastUpdate = DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
    };

    [Test]
    public async Task LoadSnapshot_NoFile_ReturnsEmpty()
    {
        var result = await _sut.LoadSnapshotAsync();
        result.Should().BeEmpty();
    }

    [Test]
    public async Task WriteThenLoadSnapshot_RoundTrips()
    {
        var issues = new Dictionary<string, Issue>
        {
            ["b"] = Make("b", "Beta"),
            ["a"] = Make("a", "Alpha"),
            ["c"] = Make("c", "Gamma"),
        };
        await _sut.WriteSnapshotAsync(issues);
        var loaded = await _sut.LoadSnapshotAsync();
        loaded.Should().HaveCount(3);
        loaded["a"].Title.Should().Be("Alpha");
        loaded["b"].Title.Should().Be("Beta");
        loaded["c"].Title.Should().Be("Gamma");
    }

    [Test]
    public async Task WriteSnapshot_LinesAreSortedById()
    {
        await _sut.WriteSnapshotAsync(new Dictionary<string, Issue>
        {
            ["zzz"] = Make("zzz", "Z"),
            ["aaa"] = Make("aaa", "A"),
            ["mmm"] = Make("mmm", "M"),
        });
        var content = _fs.File.ReadAllText(_sut.SnapshotPath);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Contain("\"id\":\"aaa\"");
        lines[1].Should().Contain("\"id\":\"mmm\"");
        lines[2].Should().Contain("\"id\":\"zzz\"");
    }

    [Test]
    public async Task WriteSnapshot_OmitsLegacyMetadataFields()
    {
        await _sut.WriteSnapshotAsync(new Dictionary<string, Issue> { ["a"] = Make("a", "A") });
        var content = _fs.File.ReadAllText(_sut.SnapshotPath);
        // The lean shape never serializes per-property *LastUpdate / *ModifiedBy.
        content.Should().NotContain("LastUpdate\":")
            .And.NotContain("ModifiedBy")
            .And.Contain("\"lastUpdate\":") // top-level lastUpdate is allowed
            .And.Contain("\"createdAt\":");
    }

    [Test]
    public async Task WriteThenLoadTombstones_RoundTrips()
    {
        var tombstones = new List<Tombstone>
        {
            new() { IssueId = "b", OriginalTitle = "B", CleanedAt = DateTimeOffset.UtcNow, CleanedBy = "bot" },
            new() { IssueId = "a", OriginalTitle = "A", CleanedAt = DateTimeOffset.UtcNow, CleanedBy = "bot" },
        };
        await _sut.WriteTombstonesAsync(tombstones);
        var loaded = await _sut.LoadTombstonesAsync();
        loaded.Should().HaveCount(2);
        // Sorted on write.
        loaded[0].IssueId.Should().Be("a");
        loaded[1].IssueId.Should().Be("b");
    }
}
