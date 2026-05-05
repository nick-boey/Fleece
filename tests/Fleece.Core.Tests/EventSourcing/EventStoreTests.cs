using System.Text.Json;
using Fleece.Core.EventSourcing;
using Fleece.Core.EventSourcing.Events;
using Fleece.Core.EventSourcing.Services;
using FluentAssertions;
using NUnit.Framework;
using Testably.Abstractions.Testing;

namespace Fleece.Core.Tests.EventSourcing;

[TestFixture]
public sealed class EventStoreTests
{
    private MockFileSystem _fs = null!;
    private string _basePath = null!;
    private Queue<string> _guids = null!;

    [SetUp]
    public void SetUp()
    {
        _fs = new MockFileSystem();
        _basePath = "/repo";
        _fs.Directory.CreateDirectory(_basePath);
        _guids = new Queue<string>();
    }

    private EventStore CreateStore(params string[] guidsToReturn)
    {
        foreach (var g in guidsToReturn)
        {
            _guids.Enqueue(g);
        }
        return new EventStore(_basePath, _fs, () => _guids.Count > 0 ? _guids.Dequeue() : Guid.NewGuid().ToString("N"));
    }

    private SetEvent SampleSet(string id = "abc") => new()
    {
        At = DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
        IssueId = id,
        Property = "title",
        Value = JsonDocument.Parse("\"x\"").RootElement,
    };

    private string ActivePath() => _fs.Path.Combine(_basePath, ".fleece", ".active-change");
    private string ChangePath(string guid) => _fs.Path.Combine(_basePath, ".fleece", "changes", $"change_{guid}.jsonl");

    [Test]
    public async Task AppendEvents_WithNoPointer_RotatesAndWritesMeta()
    {
        var store = CreateStore("guidA");
        var path = await store.AppendEventsAsync([SampleSet()]);

        path.Should().Be(ChangePath("guidA"));
        _fs.File.Exists(ActivePath()).Should().BeTrue();
        var pointer = JsonSerializer.Deserialize(_fs.File.ReadAllText(ActivePath()), EventSourcingJsonContext.Default.ActiveChangePointer);
        pointer!.Guid.Should().Be("guidA");

        var lines = _fs.File.ReadAllText(path).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        var meta = (MetaEvent)EventJsonSerializer.ParseLine(lines[0], path, 1);
        meta.Follows.Should().BeNull();
        EventJsonSerializer.ParseLine(lines[1], path, 2).Should().BeOfType<SetEvent>();
    }

    [Test]
    public async Task AppendEvents_WithExistingPointerAndFile_AppendsToSameFile()
    {
        var store = CreateStore("guidA");
        var first = await store.AppendEventsAsync([SampleSet("a")]);
        var second = await store.AppendEventsAsync([SampleSet("b")]);
        second.Should().Be(first);

        var lines = _fs.File.ReadAllText(first).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3); // meta + two sets
    }

    [Test]
    public async Task AppendEvents_WithPointerButFileMissing_RotatesToFreshGuid()
    {
        var store = CreateStore("guidA", "guidB");
        var first = await store.AppendEventsAsync([SampleSet("a")]);
        first.Should().Be(ChangePath("guidA"));

        // Simulate a branch switch that wiped the change file from the working tree.
        _fs.File.Delete(first);

        var second = await store.AppendEventsAsync([SampleSet("b")]);
        second.Should().Be(ChangePath("guidB"));

        var meta = (MetaEvent)EventJsonSerializer.ParseLine(
            _fs.File.ReadAllText(second).Split('\n', StringSplitOptions.RemoveEmptyEntries)[0], second, 1);
        // guidA's file is gone, so the DAG has no nodes — follows must be null.
        meta.Follows.Should().BeNull();
    }

    [Test]
    public async Task RotateAsync_WithExistingFiles_PointsFollowsAtLeaf()
    {
        // Pre-populate changes dir with: aaa(follows=null), bbb(follows=aaa). Leaf = bbb.
        await SeedChangeFile("aaa", followsGuid: null);
        await SeedChangeFile("bbb", followsGuid: "aaa");

        var store = CreateStore("ccc");
        var newPath = await store.RotateAsync();
        newPath.Should().Be(ChangePath("ccc"));

        var meta = await store.ReadMetaAsync(newPath);
        meta.Follows.Should().Be("bbb");
    }

    [Test]
    public async Task RotateAsync_WithMultipleRoots_PicksAlphabeticalFirstLeaf()
    {
        await SeedChangeFile("aaa", followsGuid: null);
        await SeedChangeFile("bbb", followsGuid: null);

        var store = CreateStore("zzz");
        var newPath = await store.RotateAsync();
        var meta = await store.ReadMetaAsync(newPath);
        // Both aaa and bbb are leaves (no descendants); alphabetical first is aaa.
        meta.Follows.Should().Be("aaa");
    }

    [Test]
    public async Task RotateAsync_WithMultipleLeaves_PicksAlphabeticalFirst()
    {
        // aaa is a root with descendant bbb; ccc is another root (leaf).
        // Leaves = bbb, ccc; alphabetical first = bbb.
        await SeedChangeFile("aaa", followsGuid: null);
        await SeedChangeFile("bbb", followsGuid: "aaa");
        await SeedChangeFile("ccc", followsGuid: null);

        var store = CreateStore("zzz");
        var newPath = await store.RotateAsync();
        var meta = await store.ReadMetaAsync(newPath);
        meta.Follows.Should().Be("bbb");
    }

    [Test]
    public async Task RotateAsync_NoExistingFiles_FollowsIsNull()
    {
        var store = CreateStore("guidA");
        var path = await store.RotateAsync();
        var meta = await store.ReadMetaAsync(path);
        meta.Follows.Should().BeNull();
    }

    [Test]
    public async Task GetActiveChangeFilePathAsync_NoPointer_ReturnsNull()
    {
        var store = CreateStore();
        var active = await store.GetActiveChangeFilePathAsync();
        active.Should().BeNull();
    }

    [Test]
    public async Task ReadChangeFileAsync_FirstLineNotMeta_Throws()
    {
        var path = ChangePath("xxx");
        _fs.Directory.CreateDirectory(_fs.Path.GetDirectoryName(path)!);
        _fs.File.WriteAllText(path, EventJsonSerializer.Serialize(SampleSet()) + "\n");

        var store = CreateStore();
        var act = () => store.ReadChangeFileAsync(path);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task AppendEventsAsync_RejectsMetaEvent()
    {
        var store = CreateStore("guidA");
        var act = () => store.AppendEventsAsync([new MetaEvent { Follows = null }]);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task AppendEventsAsync_RejectsEmptyList()
    {
        var store = CreateStore();
        var act = () => store.AppendEventsAsync([]);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task AppendEventsAsync_FileInHead_StillReuses()
    {
        // The spec mandates the rotation rule key off "file exists on disk", not git status.
        // We simulate "file in HEAD" simply by leaving the file present after the first append.
        var store = CreateStore("guidA");
        var first = await store.AppendEventsAsync([SampleSet("a")]);
        var second = await store.AppendEventsAsync([SampleSet("b")]);
        second.Should().Be(first);
    }

    private async Task SeedChangeFile(string guid, string? followsGuid)
    {
        var path = ChangePath(guid);
        _fs.Directory.CreateDirectory(_fs.Path.GetDirectoryName(path)!);
        var meta = EventJsonSerializer.Serialize(new MetaEvent { Follows = followsGuid });
        await _fs.File.WriteAllTextAsync(path, meta + "\n");
    }
}
