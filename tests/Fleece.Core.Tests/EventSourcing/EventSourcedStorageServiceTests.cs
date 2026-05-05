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
public sealed class EventSourcedStorageServiceTests
{
    private MockFileSystem _fs = null!;
    private string _basePath = null!;
    private SnapshotStore _snapshot = null!;
    private EventStore _eventStore = null!;
    private ReplayEngine _replay = null!;
    private ReplayCache _cache = null!;
    private StubGitContext _git = null!;
    private EventSourcedStorageService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _fs = new MockFileSystem();
        _basePath = "/repo";
        _fs.Directory.CreateDirectory(_basePath);
        _snapshot = new SnapshotStore(_basePath, _fs);
        var guidQueue = new Queue<string>(["g1", "g2", "g3", "g4", "g5", "g6"]);
        _eventStore = new EventStore(_basePath, _fs, () => guidQueue.Count > 0 ? guidQueue.Dequeue() : Guid.NewGuid().ToString("N"));
        _replay = new ReplayEngine(_eventStore);
        _cache = new ReplayCache(_basePath, _fs);
        _git = new StubGitContext();
        _sut = new EventSourcedStorageService(_snapshot, _eventStore, _replay, _cache, _git);
    }

    private static CreateEvent Create(string id, string title) => new()
    {
        At = DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
        IssueId = id,
        Data = JsonDocument.Parse($$"""
            {"title":"{{title}}","status":"open","type":"task","createdAt":"2026-04-30T10:00:00Z","lastUpdate":"2026-04-30T10:00:00Z"}
            """).RootElement,
    };

    private static SetEvent Set(string id, string property, string value) => new()
    {
        At = DateTimeOffset.Parse("2026-04-30T11:00:00Z"),
        IssueId = id,
        Property = property,
        Value = JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement,
    };

    [Test]
    public async Task GetIssues_EmptyState_ReturnsEmpty()
    {
        var result = await _sut.GetIssuesAsync();
        result.Should().BeEmpty();
    }

    [Test]
    public async Task AppendThenGetIssues_ReflectsEvents()
    {
        await _sut.AppendEventsAsync([Create("i1", "Hello")]);
        var result = await _sut.GetIssuesAsync();
        result.Should().ContainKey("i1");
        result["i1"].Title.Should().Be("Hello");
    }

    [Test]
    public async Task GetIssues_LayersEventsOnSnapshot()
    {
        await _snapshot.WriteSnapshotAsync(new Dictionary<string, Issue>
        {
            ["i1"] = new()
            {
                Id = "i1", Title = "Original", Status = IssueStatus.Open, Type = IssueType.Task,
                CreatedAt = DateTimeOffset.Parse("2026-04-29T10:00:00Z"),
                LastUpdate = DateTimeOffset.Parse("2026-04-29T10:00:00Z"),
            },
        });
        await _sut.AppendEventsAsync([Set("i1", "title", "Renamed")]);

        var result = await _sut.GetIssuesAsync();
        result["i1"].Title.Should().Be("Renamed");
    }

    [Test]
    public async Task GetIssues_NoGitRepo_DoesNotWriteCache()
    {
        // _git.Sha is null by default — no caching.
        await _sut.AppendEventsAsync([Create("i1", "Hello")]);
        await _sut.GetIssuesAsync();
        _fs.File.Exists(_fs.Path.Combine(_basePath, ".fleece", ".replay-cache")).Should().BeFalse();
    }

    [Test]
    public async Task GetIssues_CacheHit_UsesCachedCommittedState()
    {
        await _sut.AppendEventsAsync([Create("i1", "Hello")]);
        var changeFilePath = (await _eventStore.GetAllChangeFilePathsAsync()).Single();

        // Pretend the file is committed at HEAD sha "X".
        _git.Sha = "X";
        _git.CommittedFiles.Add(changeFilePath);

        // First read — cache miss, populates cache.
        var first = await _sut.GetIssuesAsync();
        first["i1"].Title.Should().Be("Hello");
        var cachePath = _fs.Path.Combine(_basePath, ".fleece", ".replay-cache");
        _fs.File.Exists(cachePath).Should().BeTrue();

        // Now corrupt the change file on disk so a real replay would fail. The cache hit
        // must short-circuit and use the cached state.
        _fs.File.WriteAllText(changeFilePath, "garbage");

        var second = await _sut.GetIssuesAsync();
        second["i1"].Title.Should().Be("Hello");
    }

    [Test]
    public async Task GetIssues_CacheMiss_AfterHeadAdvances_RecomputesAndRewrites()
    {
        await _sut.AppendEventsAsync([Create("i1", "Hello")]);
        var firstFile = (await _eventStore.GetAllChangeFilePathsAsync()).Single();
        _git.Sha = "X";
        _git.CommittedFiles.Add(firstFile);

        await _sut.GetIssuesAsync(); // populates cache for sha X

        // Add another committed event under a new HEAD sha Y. Replay must recompute.
        await _sut.AppendEventsAsync([Set("i1", "title", "Renamed")]);
        // Both files are now "committed" under the new HEAD.
        foreach (var f in await _eventStore.GetAllChangeFilePathsAsync())
        {
            _git.CommittedFiles.Add(f);
        }
        _git.Sha = "Y";

        var result = await _sut.GetIssuesAsync();
        result["i1"].Title.Should().Be("Renamed");

        var reread = await _cache.TryReadAsync();
        reread!.HeadSha.Should().Be("Y");
    }

    [Test]
    public async Task WriteSnapshot_InvalidatesCache()
    {
        _git.Sha = "X";
        await _cache.WriteAsync("X", new Dictionary<string, Issue>());
        await _sut.WriteSnapshotAsync(new Dictionary<string, Issue>(), []);
        var cached = await _cache.TryReadAsync();
        cached.Should().BeNull();
    }

    [Test]
    public async Task DeleteAllChangeFiles_RemovesFiles_AndInvalidatesCache()
    {
        await _sut.AppendEventsAsync([Create("i1", "T")]);
        _git.Sha = "X";
        await _cache.WriteAsync("X", new Dictionary<string, Issue>());

        await _sut.DeleteAllChangeFilesAsync();

        (await _eventStore.GetAllChangeFilePathsAsync()).Should().BeEmpty();
        var cached = await _cache.TryReadAsync();
        cached.Should().BeNull();
    }

    private sealed class StubGitContext : IEventGitContext
    {
        public string? Sha { get; set; }
        public HashSet<string> CommittedFiles { get; } = new(StringComparer.Ordinal);
        public string? GetHeadSha() => Sha;
        public bool IsFileCommittedAtHead(string filePath) => CommittedFiles.Contains(filePath);
        public int? GetFirstCommitOrdinal(string filePath) => null;
    }
}
