using Fleece.Core.Models;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Fleece.Core.Tests.TestHelpers;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class CleanServiceTests
{
    private IStorageService _storage = null!;
    private IGitConfigService _gitConfigService = null!;
    private CleanService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _storage = Substitute.For<IStorageService>();
        _gitConfigService = Substitute.For<IGitConfigService>();
        _gitConfigService.GetUserName().Returns("Test User");
        _storage.LoadTombstonesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Tombstone>>([]));
        _sut = new CleanService(_storage, _gitConfigService);
    }

    [Test]
    public async Task CleanAsync_CleansOnlyDeletedByDefault()
    {
        var deletedIssue = new IssueBuilder().WithId("del001").WithStatus(IssueStatus.Deleted).Build();
        var openIssue = new IssueBuilder().WithId("opn001").WithStatus(IssueStatus.Open).Build();
        var completeIssue = new IssueBuilder().WithId("cmp001").WithStatus(IssueStatus.Complete).Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([deletedIssue, openIssue, completeIssue]);

        var result = await _sut.CleanAsync();

        result.CleanedTombstones.Should().HaveCount(1);
        result.CleanedTombstones[0].IssueId.Should().Be("del001");
    }

    [Test]
    public async Task CleanAsync_DoesNotCleanNonDeletedUnlessFlagsSet()
    {
        var completeIssue = new IssueBuilder().WithId("cmp001").WithStatus(IssueStatus.Complete).Build();
        var closedIssue = new IssueBuilder().WithId("cls001").WithStatus(IssueStatus.Closed).Build();
        var archivedIssue = new IssueBuilder().WithId("arc001").WithStatus(IssueStatus.Archived).Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([completeIssue, closedIssue, archivedIssue]);

        var result = await _sut.CleanAsync();

        result.CleanedTombstones.Should().BeEmpty();
    }

    [Test]
    public async Task CleanAsync_IncludesCompleteWhenFlagSet()
    {
        var completeIssue = new IssueBuilder().WithId("cmp001").WithStatus(IssueStatus.Complete).Build();
        var openIssue = new IssueBuilder().WithId("opn001").WithStatus(IssueStatus.Open).Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([completeIssue, openIssue]);

        var result = await _sut.CleanAsync(includeComplete: true);

        result.CleanedTombstones.Should().HaveCount(1);
        result.CleanedTombstones[0].IssueId.Should().Be("cmp001");
    }

    [Test]
    public async Task CleanAsync_IncludesClosedWhenFlagSet()
    {
        var closedIssue = new IssueBuilder().WithId("cls001").WithStatus(IssueStatus.Closed).Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([closedIssue]);

        var result = await _sut.CleanAsync(includeClosed: true);

        result.CleanedTombstones.Should().HaveCount(1);
        result.CleanedTombstones[0].IssueId.Should().Be("cls001");
    }

    [Test]
    public async Task CleanAsync_IncludesArchivedWhenFlagSet()
    {
        var archivedIssue = new IssueBuilder().WithId("arc001").WithStatus(IssueStatus.Archived).Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([archivedIssue]);

        var result = await _sut.CleanAsync(includeArchived: true);

        result.CleanedTombstones.Should().HaveCount(1);
        result.CleanedTombstones[0].IssueId.Should().Be("arc001");
    }

    [Test]
    public async Task CleanAsync_CreatesTombstonesWithCorrectFields()
    {
        var deletedIssue = new IssueBuilder()
            .WithId("del001")
            .WithTitle("My deleted issue")
            .WithStatus(IssueStatus.Deleted)
            .Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([deletedIssue]);

        var result = await _sut.CleanAsync();

        result.CleanedTombstones.Should().HaveCount(1);
        var tombstone = result.CleanedTombstones[0];
        tombstone.IssueId.Should().Be("del001");
        tombstone.OriginalTitle.Should().Be("My deleted issue");
        tombstone.CleanedBy.Should().Be("Test User");
        tombstone.CleanedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task CleanAsync_StripsLinkedIssuesReferences()
    {
        var deletedIssue = new IssueBuilder().WithId("del001").WithStatus(IssueStatus.Deleted).Build();
        var referencingIssue = new IssueBuilder()
            .WithId("opn001")
            .WithStatus(IssueStatus.Open)
            .WithLinkedIssues("del001", "opn002")
            .Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([deletedIssue, referencingIssue]);

        var result = await _sut.CleanAsync();

        result.StrippedReferences.Should().HaveCount(1);
        result.StrippedReferences[0].IssueId.Should().Be("del001");
        result.StrippedReferences[0].ReferencingIssueId.Should().Be("opn001");
        result.StrippedReferences[0].ReferenceType.Should().Be("LinkedIssues");

        await _storage.Received(1).SaveIssuesAsync(
            Arg.Is<IReadOnlyList<Issue>>(issues =>
                issues.Count == 1 &&
                issues[0].LinkedIssues.Count == 1 &&
                issues[0].LinkedIssues[0] == "opn002"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CleanAsync_StripsParentIssuesReferences()
    {
        var deletedIssue = new IssueBuilder().WithId("del001").WithStatus(IssueStatus.Deleted).Build();
        var childIssue = new IssueBuilder()
            .WithId("opn001")
            .WithStatus(IssueStatus.Open)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "del001", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "opn002", SortOrder = "bbb" })
            .Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([deletedIssue, childIssue]);

        var result = await _sut.CleanAsync();

        result.StrippedReferences.Should().HaveCount(1);
        result.StrippedReferences[0].IssueId.Should().Be("del001");
        result.StrippedReferences[0].ReferencingIssueId.Should().Be("opn001");
        result.StrippedReferences[0].ReferenceType.Should().Be("ParentIssues");

        await _storage.Received(1).SaveIssuesAsync(
            Arg.Is<IReadOnlyList<Issue>>(issues =>
                issues.Count == 1 &&
                issues[0].ParentIssues.Count == 1 &&
                issues[0].ParentIssues[0].ParentIssue == "opn002"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CleanAsync_RespectsStripReferencesFalse()
    {
        var deletedIssue = new IssueBuilder().WithId("del001").WithStatus(IssueStatus.Deleted).Build();
        var referencingIssue = new IssueBuilder()
            .WithId("opn001")
            .WithStatus(IssueStatus.Open)
            .WithLinkedIssues("del001")
            .Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([deletedIssue, referencingIssue]);

        var result = await _sut.CleanAsync(stripReferences: false);

        result.StrippedReferences.Should().BeEmpty();

        await _storage.Received(1).SaveIssuesAsync(
            Arg.Is<IReadOnlyList<Issue>>(issues =>
                issues.Count == 1 &&
                issues[0].LinkedIssues.Count == 1 &&
                issues[0].LinkedIssues[0] == "del001"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CleanAsync_DryRunReturnsResultsWithoutSaving()
    {
        var deletedIssue = new IssueBuilder().WithId("del001").WithStatus(IssueStatus.Deleted).Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([deletedIssue]);

        var result = await _sut.CleanAsync(dryRun: true);

        result.CleanedTombstones.Should().HaveCount(1);

        await _storage.DidNotReceive().SaveIssuesAsync(
            Arg.Any<IReadOnlyList<Issue>>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().SaveTombstonesAsync(
            Arg.Any<IReadOnlyList<Tombstone>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CleanAsync_ReturnsEmptyResultWhenNothingToClean()
    {
        var openIssue = new IssueBuilder().WithId("opn001").WithStatus(IssueStatus.Open).Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([openIssue]);

        var result = await _sut.CleanAsync();

        result.CleanedTombstones.Should().BeEmpty();
        result.StrippedReferences.Should().BeEmpty();

        await _storage.DidNotReceive().SaveIssuesAsync(
            Arg.Any<IReadOnlyList<Issue>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CleanAsync_MergesNewTombstonesWithExisting()
    {
        var deletedIssue = new IssueBuilder().WithId("del002").WithStatus(IssueStatus.Deleted).Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([deletedIssue]);

        var existingTombstone = new Tombstone
        {
            IssueId = "del001",
            OriginalTitle = "Previous deleted issue",
            CleanedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CleanedBy = "Old User"
        };

        _storage.LoadTombstonesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Tombstone> { existingTombstone });

        await _sut.CleanAsync();

        await _storage.Received(1).SaveTombstonesAsync(
            Arg.Is<IReadOnlyList<Tombstone>>(ts =>
                ts.Count == 2 &&
                ts.Any(t => t.IssueId == "del001") &&
                ts.Any(t => t.IssueId == "del002")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CleanAsync_SavesRemainingIssuesWithoutCleanedOnes()
    {
        var deletedIssue = new IssueBuilder().WithId("del001").WithStatus(IssueStatus.Deleted).Build();
        var openIssue = new IssueBuilder().WithId("opn001").WithStatus(IssueStatus.Open).Build();
        var progressIssue = new IssueBuilder().WithId("prg001").WithStatus(IssueStatus.Progress).Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([deletedIssue, openIssue, progressIssue]);

        await _sut.CleanAsync();

        await _storage.Received(1).SaveIssuesAsync(
            Arg.Is<IReadOnlyList<Issue>>(issues =>
                issues.Count == 2 &&
                issues.All(i => i.Id != "del001")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CleanAsync_CleansMultipleStatusesWhenAllFlagsSet()
    {
        var deletedIssue = new IssueBuilder().WithId("del001").WithStatus(IssueStatus.Deleted).Build();
        var completeIssue = new IssueBuilder().WithId("cmp001").WithStatus(IssueStatus.Complete).Build();
        var closedIssue = new IssueBuilder().WithId("cls001").WithStatus(IssueStatus.Closed).Build();
        var archivedIssue = new IssueBuilder().WithId("arc001").WithStatus(IssueStatus.Archived).Build();
        var openIssue = new IssueBuilder().WithId("opn001").WithStatus(IssueStatus.Open).Build();

        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([deletedIssue, completeIssue, closedIssue, archivedIssue, openIssue]);

        var result = await _sut.CleanAsync(
            includeComplete: true,
            includeClosed: true,
            includeArchived: true);

        result.CleanedTombstones.Should().HaveCount(4);
        result.CleanedTombstones.Select(t => t.IssueId)
            .Should().BeEquivalentTo(["del001", "cmp001", "cls001", "arc001"]);
    }
}
