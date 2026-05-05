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
public sealed class ReplayEngineTests
{
    private MockFileSystem _fs = null!;
    private string _basePath = null!;
    private EventStore _eventStore = null!;
    private ReplayEngine _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _fs = new MockFileSystem();
        _basePath = "/repo";
        _fs.Directory.CreateDirectory(_basePath);
        _eventStore = new EventStore(_basePath, _fs, () => Guid.NewGuid().ToString("N"));
        _engine = new ReplayEngine(_eventStore);
    }

    private string ChangePath(string guid) => _fs.Path.Combine(_basePath, ".fleece", "changes", $"change_{guid}.jsonl");

    private async Task SeedFileAsync(string guid, string? follows, params IssueEvent[] events)
    {
        var path = ChangePath(guid);
        _fs.Directory.CreateDirectory(_fs.Path.GetDirectoryName(path)!);
        var lines = new List<string> { EventJsonSerializer.Serialize(new MetaEvent { Follows = follows }) };
        lines.AddRange(events.Select(EventJsonSerializer.Serialize));
        await _fs.File.WriteAllTextAsync(path, string.Join('\n', lines) + "\n");
    }

    private static CreateEvent Create(string id, string title, IssueStatus status = IssueStatus.Open, IssueType type = IssueType.Task, DateTimeOffset? at = null) => new()
    {
        At = at ?? DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
        IssueId = id,
        Data = JsonDocument.Parse($$"""
            {"title":"{{title}}","status":"{{status.ToString().ToLowerInvariant()}}","type":"{{type.ToString().ToLowerInvariant()}}","createdAt":"2026-04-30T10:00:00Z","lastUpdate":"2026-04-30T10:00:00Z"}
            """).RootElement,
    };

    private static SetEvent Set(string id, string property, string value, DateTimeOffset? at = null) => new()
    {
        At = at ?? DateTimeOffset.Parse("2026-04-30T11:00:00Z"),
        IssueId = id,
        Property = property,
        Value = JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement,
    };

    private static AddEvent Add(string id, string property, JsonElement value) => new()
    {
        At = DateTimeOffset.Parse("2026-04-30T11:00:00Z"),
        IssueId = id,
        Property = property,
        Value = value,
    };

    private static RemoveEvent Remove(string id, string property, JsonElement value) => new()
    {
        At = DateTimeOffset.Parse("2026-04-30T11:00:00Z"),
        IssueId = id,
        Property = property,
        Value = value,
    };

    [Test]
    public async Task Replay_LinearChain_AppliesEventsInOrder()
    {
        // aaa: create + set title=A. bbb (follows=aaa): set title=B. ccc (follows=bbb): set title=C.
        await SeedFileAsync("aaa", null, Create("i1", "A"));
        await SeedFileAsync("bbb", "aaa", Set("i1", "title", "B"));
        await SeedFileAsync("ccc", "bbb", Set("i1", "title", "C"));

        var paths = (await _eventStore.GetAllChangeFilePathsAsync()).ToList();
        var result = await _engine.ReplayAsync(new Dictionary<string, Issue>(), paths);

        result.Should().ContainKey("i1");
        result["i1"].Title.Should().Be("C");
    }

    [Test]
    public async Task Replay_MultiMachineSquash_FollowsPointerOrdersCorrectly()
    {
        // Spec scenario: machine 1 wrote aaa with title=foo then title=bar.
        // Machine 2 (after pull) wrote bbb (follows=aaa) with title=baz.
        // After squash, both files exist on main; replay must produce title=baz.
        await SeedFileAsync("aaa", null,
            Create("i1", "foo"),
            Set("i1", "title", "foo"),
            Set("i1", "title", "bar"));
        await SeedFileAsync("bbb", "aaa", Set("i1", "title", "baz"));

        var paths = (await _eventStore.GetAllChangeFilePathsAsync()).ToList();
        var result = await _engine.ReplayAsync(new Dictionary<string, Issue>(), paths);
        result["i1"].Title.Should().Be("baz");
    }

    [Test]
    public async Task Replay_ParallelBranches_TiebreaksByCommitOrder()
    {
        // aaa and bbb are both roots. Commit order says bbb came first, so bbb wins.
        await SeedFileAsync("aaa", null, Create("i1", "from-aaa"), Set("i1", "title", "from-aaa"));
        await SeedFileAsync("bbb", null, Create("i1", "from-bbb"), Set("i1", "title", "from-bbb"));

        var paths = (await _eventStore.GetAllChangeFilePathsAsync()).ToList();
        var commitOrder = new StubCommitOrder
        {
            { ChangePath("aaa"), 1 },
            { ChangePath("bbb"), 0 },
        };

        var result = await _engine.ReplayAsync(new Dictionary<string, Issue>(), paths, commitOrder);
        // bbb commits first (ord 0), then aaa (ord 1) → aaa's events run last → title=from-aaa.
        result["i1"].Title.Should().Be("from-aaa");
    }

    [Test]
    public async Task Replay_ParallelBranches_NoCommitOrder_FallsBackToGuidAlphabetical()
    {
        await SeedFileAsync("aaa", null, Create("i1", "from-aaa"), Set("i1", "title", "from-aaa"));
        await SeedFileAsync("bbb", null, Create("i1", "from-bbb"), Set("i1", "title", "from-bbb"));

        var paths = (await _eventStore.GetAllChangeFilePathsAsync()).ToList();
        var result = await _engine.ReplayAsync(new Dictionary<string, Issue>(), paths);
        // No commit order → alphabetical: aaa first, bbb second → bbb's events apply last.
        result["i1"].Title.Should().Be("from-bbb");
    }

    [Test]
    public async Task Replay_DanglingFollows_TreatsAsRoot()
    {
        // bbb's follows points to "missing" which doesn't exist. It should be treated as a root,
        // ordered by commit/alphabetical alongside aaa.
        await SeedFileAsync("aaa", null, Create("i1", "first"), Set("i1", "title", "first"));
        await SeedFileAsync("bbb", "missing", Create("i1", "second"), Set("i1", "title", "second"));

        var paths = (await _eventStore.GetAllChangeFilePathsAsync()).ToList();
        var result = await _engine.ReplayAsync(new Dictionary<string, Issue>(), paths);
        // Both roots → alphabetical: aaa first, bbb second → bbb wins.
        result["i1"].Title.Should().Be("second");
    }

    [Test]
    public async Task Replay_AddIdempotent_DoesNotDuplicateTags()
    {
        var foo = JsonDocument.Parse("\"foo\"").RootElement;
        await SeedFileAsync("aaa", null,
            Create("i1", "T"),
            Add("i1", "tags", foo),
            Add("i1", "tags", foo));

        var paths = (await _eventStore.GetAllChangeFilePathsAsync()).ToList();
        var result = await _engine.ReplayAsync(new Dictionary<string, Issue>(), paths);
        result["i1"].Tags.Should().ContainSingle().Which.Should().Be("foo");
    }

    [Test]
    public async Task Replay_RemoveIdempotent_NoErrorWhenAbsent()
    {
        var foo = JsonDocument.Parse("\"foo\"").RootElement;
        await SeedFileAsync("aaa", null,
            Create("i1", "T"),
            Remove("i1", "tags", foo));

        var paths = (await _eventStore.GetAllChangeFilePathsAsync()).ToList();
        var result = await _engine.ReplayAsync(new Dictionary<string, Issue>(), paths);
        result["i1"].Tags.Should().BeEmpty();
    }

    [Test]
    public async Task Replay_ParentIssues_RemoveByKey_DropsRegardlessOfMetadata()
    {
        var fullRef = JsonDocument.Parse("""{"parentIssue":"P1","lexOrder":"aaa","active":true}""").RootElement;
        var keyOnly = JsonDocument.Parse("""{"parentIssue":"P1"}""").RootElement;
        await SeedFileAsync("aaa", null,
            Create("c1", "Child"),
            Add("c1", "parentIssues", fullRef),
            Remove("c1", "parentIssues", keyOnly));

        var paths = (await _eventStore.GetAllChangeFilePathsAsync()).ToList();
        var result = await _engine.ReplayAsync(new Dictionary<string, Issue>(), paths);
        result["c1"].ParentIssues.Should().BeEmpty();
    }

    [Test]
    public async Task Replay_HardDelete_DropsIssueFromState()
    {
        await SeedFileAsync("aaa", null, Create("i1", "T"));
        await SeedFileAsync("bbb", "aaa", new HardDeleteEvent
        {
            At = DateTimeOffset.Parse("2026-04-30T12:00:00Z"),
            IssueId = "i1",
        });

        var paths = (await _eventStore.GetAllChangeFilePathsAsync()).ToList();
        var result = await _engine.ReplayAsync(new Dictionary<string, Issue>(), paths);
        result.Should().NotContainKey("i1");
    }

    [Test]
    public async Task Replay_StartingFromSnapshot_AppliesEventsOnTop()
    {
        var existing = new Issue
        {
            Id = "i1",
            Title = "Original",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            CreatedAt = DateTimeOffset.Parse("2026-04-29T10:00:00Z"),
            LastUpdate = DateTimeOffset.Parse("2026-04-29T10:00:00Z"),
        };
        await SeedFileAsync("aaa", null, Set("i1", "title", "Renamed"));

        var paths = (await _eventStore.GetAllChangeFilePathsAsync()).ToList();
        var result = await _engine.ReplayAsync(
            new Dictionary<string, Issue> { ["i1"] = existing },
            paths);
        result["i1"].Title.Should().Be("Renamed");
    }

    [Test]
    public async Task Replay_SquashEquivalence_PostSquashMatchesPreSquash()
    {
        // Three sequential change files = "pre-squash" view.
        await SeedFileAsync("aaa", null, Create("i1", "T"), Set("i1", "title", "v1"));
        await SeedFileAsync("bbb", "aaa", Set("i1", "title", "v2"));
        await SeedFileAsync("ccc", "bbb", Set("i1", "title", "v3"));
        var preSquashPaths = (await _eventStore.GetAllChangeFilePathsAsync()).ToList();
        var preSquash = await _engine.ReplayAsync(new Dictionary<string, Issue>(), preSquashPaths);

        // "Post-squash" — same events present, but commit ordinals collapse to a single value.
        // The follows-DAG still orders them correctly.
        var postCommitOrder = new StubCommitOrder();
        foreach (var p in preSquashPaths)
        {
            postCommitOrder[p] = 0; // all "from the squash commit"
        }
        var postSquash = await _engine.ReplayAsync(new Dictionary<string, Issue>(), preSquashPaths, postCommitOrder);

        postSquash["i1"].Title.Should().Be(preSquash["i1"].Title);
        postSquash["i1"].Title.Should().Be("v3");
    }

    private sealed class StubCommitOrder : Dictionary<string, int>, IChangeFileCommitOrder
    {
        public int? GetFirstCommitOrdinal(string filePath) =>
            TryGetValue(filePath, out var v) ? v : null;
    }
}
