using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class MergeServiceTests
{
    private IStorageService _storage = null!;
    private IConflictService _conflictService = null!;
    private IJsonlSerializer _serializer = null!;
    private MergeService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _storage = Substitute.For<IStorageService>();
        _conflictService = Substitute.For<IConflictService>();
        _serializer = new JsonlSerializer();
        _sut = new MergeService(_storage, _conflictService, _serializer);
    }

    [Test]
    public async Task FindAndResolveDuplicatesAsync_ReturnsEmpty_WhenNoDuplicates()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FindAndResolveDuplicatesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task FindAndResolveDuplicatesAsync_DetectsDuplicates()
    {
        var older = DateTimeOffset.UtcNow.AddHours(-1);
        var newer = DateTimeOffset.UtcNow;

        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Old", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = older },
            new() { Id = "abc123", Title = "New", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = newer }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FindAndResolveDuplicatesAsync();

        result.Should().HaveCount(1);
        result[0].IssueId.Should().Be("abc123");
        result[0].OlderVersion.Title.Should().Be("Old");
        result[0].NewerVersion.Title.Should().Be("New");
    }

    [Test]
    public async Task FindAndResolveDuplicatesAsync_KeepsNewerVersion()
    {
        var older = DateTimeOffset.UtcNow.AddHours(-1);
        var newer = DateTimeOffset.UtcNow;

        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Old", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = older },
            new() { Id = "abc123", Title = "New", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = newer }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        await _sut.FindAndResolveDuplicatesAsync();

        await _storage.Received(1).SaveIssuesAsync(
            Arg.Is<IReadOnlyList<Issue>>(list =>
                list.Count == 1 &&
                list[0].Title == "New"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FindAndResolveDuplicatesAsync_AddsConflict()
    {
        var older = DateTimeOffset.UtcNow.AddHours(-1);
        var newer = DateTimeOffset.UtcNow;

        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Old", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = older },
            new() { Id = "abc123", Title = "New", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = newer }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        await _sut.FindAndResolveDuplicatesAsync();

        await _conflictService.Received(1).AddAsync(
            Arg.Is<ConflictRecord>(c => c.IssueId == "abc123"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FindAndResolveDuplicatesAsync_HandlesMultipleDuplicates()
    {
        var time1 = DateTimeOffset.UtcNow.AddHours(-2);
        var time2 = DateTimeOffset.UtcNow.AddHours(-1);
        var time3 = DateTimeOffset.UtcNow;

        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "V1", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = time1 },
            new() { Id = "abc123", Title = "V2", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = time2 },
            new() { Id = "abc123", Title = "V3", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = time3 }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FindAndResolveDuplicatesAsync();

        result.Should().HaveCount(2);
        await _conflictService.Received(2).AddAsync(Arg.Any<ConflictRecord>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FindAndResolveDuplicatesAsync_DoesNotSaveWhenNoDuplicates()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        await _sut.FindAndResolveDuplicatesAsync();

        await _storage.DidNotReceive().SaveIssuesAsync(Arg.Any<IReadOnlyList<Issue>>(), Arg.Any<CancellationToken>());
    }
}
