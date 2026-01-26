using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class MergeServiceTests
{
    private IStorageService _storage = null!;
    private IGitConfigService _gitConfigService = null!;
    private IJsonlSerializer _serializer = null!;
    private MergeService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _storage = Substitute.For<IStorageService>();
        _gitConfigService = Substitute.For<IGitConfigService>();
        _gitConfigService.GetUserName().Returns("Test User");
        _serializer = new JsonlSerializer();
        _sut = new MergeService(_storage, _gitConfigService, _serializer);
    }

    private void SetupStorageMock(IReadOnlyList<Issue> issues)
    {
        var filePath = "/mock/issues_abc123.jsonl";
        _storage.GetAllIssueFilesAsync(Arg.Any<CancellationToken>()).Returns([filePath]);
        _storage.LoadIssuesFromFileAsync(filePath, Arg.Any<CancellationToken>()).Returns(issues);
    }

    [Test]
    public async Task FindAndResolveDuplicatesAsync_ReturnsEmpty_WhenNoDuplicates()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("a").WithTitle("A").Build(),
            new IssueBuilder().WithId("b").WithTitle("B").Build()
        };
        SetupStorageMock(issues);

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
            new IssueBuilder().WithId("abc123").WithTitle("Old").WithLastUpdate(older).Build(),
            new IssueBuilder().WithId("abc123").WithTitle("New").WithLastUpdate(newer).Build()
        };
        SetupStorageMock(issues);

        var result = await _sut.FindAndResolveDuplicatesAsync();

        result.Should().HaveCount(1);
        result[0].IssueId.Should().Be("abc123");
        result[0].Type.Should().Be(ChangeType.Merged);
    }

    [Test]
    public async Task FindAndResolveDuplicatesAsync_KeepsNewerVersion()
    {
        var older = DateTimeOffset.UtcNow.AddHours(-1);
        var newer = DateTimeOffset.UtcNow;

        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("abc123").WithTitle("Old").WithLastUpdate(older).Build(),
            new IssueBuilder().WithId("abc123").WithTitle("New").WithLastUpdate(newer).Build()
        };
        SetupStorageMock(issues);

        await _sut.FindAndResolveDuplicatesAsync();

        await _storage.Received(1).SaveIssuesWithHashAsync(
            Arg.Is<IReadOnlyList<Issue>>(list =>
                list.Count == 1 &&
                list[0].Title == "New"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FindAndResolveDuplicatesAsync_AddsChangeRecord()
    {
        var older = DateTimeOffset.UtcNow.AddHours(-1);
        var newer = DateTimeOffset.UtcNow;

        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("abc123").WithTitle("Old").WithLastUpdate(older).Build(),
            new IssueBuilder().WithId("abc123").WithTitle("New").WithLastUpdate(newer).Build()
        };
        SetupStorageMock(issues);

        await _sut.FindAndResolveDuplicatesAsync();

        await _storage.Received(1).SaveChangesAsync(
            Arg.Is<IReadOnlyList<ChangeRecord>>(changes =>
                changes.Any(c => c.IssueId == "abc123" && c.Type == ChangeType.Merged)),
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
            new IssueBuilder().WithId("abc123").WithTitle("V1").WithLastUpdate(time1).Build(),
            new IssueBuilder().WithId("abc123").WithTitle("V2").WithLastUpdate(time2).Build(),
            new IssueBuilder().WithId("abc123").WithTitle("V3").WithLastUpdate(time3).Build()
        };
        SetupStorageMock(issues);

        var result = await _sut.FindAndResolveDuplicatesAsync();

        // Property-level merge combines all into one merged result, so only 1 change record
        result.Should().HaveCount(1);
        await _storage.Received(1).SaveChangesAsync(Arg.Any<IReadOnlyList<ChangeRecord>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FindAndResolveDuplicatesAsync_ConsolidatesFilesWhenNoDuplicates()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("a").WithTitle("A").Build()
        };
        SetupStorageMock(issues);

        await _sut.FindAndResolveDuplicatesAsync();

        // Still saves once because we have files to consolidate
        await _storage.Received(1).SaveIssuesWithHashAsync(Arg.Any<IReadOnlyList<Issue>>(), Arg.Any<CancellationToken>());
        // Changes should also be consolidated even when there are no issue duplicates
        await _storage.Received(1).SaveChangesAsync(Arg.Any<IReadOnlyList<ChangeRecord>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FindAndResolveDuplicatesAsync_AlwaysConsolidatesChanges()
    {
        // Arrange: Single issue with no duplicates, but changes exist
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("a").WithTitle("A").Build()
        };
        SetupStorageMock(issues);

        var existingChanges = new List<ChangeRecord>
        {
            new()
            {
                ChangeId = Guid.NewGuid(),
                IssueId = "a",
                Type = ChangeType.Created,
                ChangedBy = "user",
                ChangedAt = DateTimeOffset.UtcNow.AddHours(-1),
                PropertyChanges = []
            }
        };
        _storage.LoadChangesAsync(Arg.Any<CancellationToken>()).Returns(existingChanges);

        // Act
        await _sut.FindAndResolveDuplicatesAsync();

        // Assert: Changes should be consolidated even without issue duplicates
        await _storage.Received(1).SaveChangesAsync(
            Arg.Is<IReadOnlyList<ChangeRecord>>(changes => changes.Count == 1),
            Arg.Any<CancellationToken>());
    }
}
