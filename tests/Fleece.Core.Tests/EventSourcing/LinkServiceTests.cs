using System.Text.Json;
using Fleece.Core.EventSourcing.Events;
using Fleece.Core.EventSourcing.Services;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NUnit.Framework;
using Testably.Abstractions.Testing;

namespace Fleece.Core.Tests.EventSourcing;

[TestFixture]
public sealed class LinkServiceTests
{
    private MockFileSystem _fs = null!;
    private string _basePath = null!;
    private EventStore _eventStore = null!;
    private StubGitService _git = null!;
    private Queue<string> _guids = null!;

    [SetUp]
    public void SetUp()
    {
        _fs = new MockFileSystem();
        _basePath = "/repo";
        _fs.Directory.CreateDirectory(_basePath);
        _eventStore = new EventStore(_basePath, _fs);
        _git = new StubGitService();
        _guids = new Queue<string>();
    }

    private LinkService CreateService(params string[] guidsToReturn)
    {
        foreach (var g in guidsToReturn)
        {
            _guids.Enqueue(g);
        }
        return new LinkService(
            _basePath,
            _fs,
            _eventStore,
            _git,
            () => _guids.Count > 0 ? _guids.Dequeue() : Guid.NewGuid().ToString("N"));
    }

    private string ChangePath(string guid) =>
        _fs.Path.Combine(_basePath, ".fleece", "changes", $"change_{guid}.jsonl");

    private async Task SeedChangeFile(string guid, IReadOnlyList<string> follows)
    {
        var path = ChangePath(guid);
        _fs.Directory.CreateDirectory(_fs.Path.GetDirectoryName(path)!);
        var meta = EventJsonSerializer.Serialize(new MetaEvent { Follows = follows });
        await _fs.File.WriteAllTextAsync(path, meta + "\n");
    }

    private void CreateMergeHead(params string[] sideShas)
    {
        var dir = _fs.Path.Combine(_basePath, ".git");
        _fs.Directory.CreateDirectory(dir);
        _fs.File.WriteAllText(_fs.Path.Combine(dir, "MERGE_HEAD"), string.Join('\n', sideShas) + "\n");
    }

    [Test]
    public async Task LinkService_no_MERGE_HEAD_is_noop()
    {
        var svc = CreateService("g1");
        var result = await svc.CreateMergeMarkerAsync();

        result.MarkerCreated.Should().BeFalse();
        result.MarkerGuid.Should().BeNull();
        result.MarkerPath.Should().BeNull();
        var changesDir = _fs.Path.Combine(_basePath, ".fleece", "changes");
        if (_fs.Directory.Exists(changesDir))
        {
            _fs.Directory.GetFiles(changesDir).Should().BeEmpty();
        }
    }

    [Test]
    public async Task LinkService_no_change_files_is_noop_even_during_merge()
    {
        CreateMergeHead("abc123");

        var svc = CreateService("g1");
        var result = await svc.CreateMergeMarkerAsync();

        result.MarkerCreated.Should().BeFalse();
        result.Parents.Should().BeEmpty();
    }

    [Test]
    public async Task LinkService_one_MERGE_HEAD_with_two_parallel_leaves_writes_two_parent_marker()
    {
        CreateMergeHead("abc123");
        await SeedChangeFile("aaa", []);
        await SeedChangeFile("bbb", []);

        var svc = CreateService("marker1");
        var result = await svc.CreateMergeMarkerAsync();

        result.MarkerCreated.Should().BeTrue();
        result.MarkerGuid.Should().Be("marker1");
        result.Parents.Should().Equal("aaa", "bbb");

        var meta = await _eventStore.ReadMetaAsync(ChangePath("marker1"));
        meta.Follows.Should().Equal("aaa", "bbb");

        // The service must stage the marker via git.
        _git.RanCommands.Should().Contain(cmd => cmd.Contains("add") && cmd.Contains("marker1"));
    }

    [Test]
    public async Task LinkService_octopus_merge_with_three_leaves_writes_three_parent_marker()
    {
        CreateMergeHead("abc", "def", "ghi");
        await SeedChangeFile("aaa", []);
        await SeedChangeFile("bbb", []);
        await SeedChangeFile("ccc", []);

        var svc = CreateService("marker1");
        var result = await svc.CreateMergeMarkerAsync();

        result.MarkerCreated.Should().BeTrue();
        result.Parents.Should().Equal("aaa", "bbb", "ccc");

        var meta = await _eventStore.ReadMetaAsync(ChangePath("marker1"));
        meta.Follows.Should().Equal("aaa", "bbb", "ccc");
    }

    [Test]
    public async Task LinkService_single_chain_writes_single_parent_marker_serialised_as_scalar()
    {
        CreateMergeHead("abc");
        await SeedChangeFile("aaa", []);
        await SeedChangeFile("bbb", ["aaa"]); // bbb chains from aaa, so leaf = bbb only

        var svc = CreateService("marker1");
        var result = await svc.CreateMergeMarkerAsync();

        result.MarkerCreated.Should().BeTrue();
        result.Parents.Should().Equal("bbb");

        // On disk, the single-element list should serialise as a scalar string.
        var raw = _fs.File.ReadAllText(ChangePath("marker1"));
        raw.Should().Contain("\"follows\":\"bbb\"");
    }

    private sealed class StubGitService : IGitService
    {
        public List<string> RanCommands { get; } = [];

        public bool IsGitAvailable() => true;
        public bool IsGitRepository() => true;
        public bool HasFleeceChanges() => false;
        public GitOperationResult StageFleeceDirectory() => GitOperationResult.Ok();
        public GitOperationResult Commit(string message) => GitOperationResult.Ok();
        public GitOperationResult Push() => GitOperationResult.Ok();
        public GitOperationResult CommitFleeceChanges(string message) => GitOperationResult.Ok();
        public GitOperationResult CommitAndPushFleeceChanges(string message) => GitOperationResult.Ok();
        public string? GetCurrentBranch() => "main";
        public (int ExitCode, string Output, string Error) RunGitCommand(string arguments)
        {
            RanCommands.Add(arguments);
            return (0, string.Empty, string.Empty);
        }
    }
}
