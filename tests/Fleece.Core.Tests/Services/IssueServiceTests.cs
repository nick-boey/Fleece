using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class IssueServiceTests
{
    private IStorageService _storage = null!;
    private IIdGenerator _idGenerator = null!;
    private IGitConfigService _gitConfigService = null!;
    private IChangeService _changeService = null!;
    private IssueService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _storage = Substitute.For<IStorageService>();
        _idGenerator = Substitute.For<IIdGenerator>();
        _gitConfigService = Substitute.For<IGitConfigService>();
        _changeService = Substitute.For<IChangeService>();
        _gitConfigService.GetUserName().Returns("Test User");
        _sut = new IssueService(_storage, _idGenerator, _gitConfigService, _changeService);
    }

    [Test]
    public async Task CreateAsync_GeneratesIdFromTitle()
    {
        _idGenerator.Generate("Test Issue").Returns("abc123");

        var result = await _sut.CreateAsync("Test Issue", IssueType.Task);

        result.Id.Should().Be("abc123");
        _idGenerator.Received(1).Generate("Test Issue");
    }

    [Test]
    public async Task CreateAsync_AppendsThroughStorage()
    {
        _idGenerator.Generate(Arg.Any<string>()).Returns("abc123");

        await _sut.CreateAsync("Test Issue", IssueType.Bug);

        await _storage.Received(1).AppendIssueAsync(
            Arg.Is<Issue>(i => i.Id == "abc123" && i.Title == "Test Issue" && i.Type == IssueType.Bug),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateAsync_SetsDefaultStatusToOpen()
    {
        _idGenerator.Generate(Arg.Any<string>()).Returns("abc123");

        var result = await _sut.CreateAsync("Test Issue", IssueType.Task);

        result.Status.Should().Be(IssueStatus.Open);
    }

    [Test]
    public async Task CreateAsync_SetsAllProvidedFields()
    {
        _idGenerator.Generate(Arg.Any<string>()).Returns("abc123");

        var result = await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Feature,
            description: "A description",
            status: IssueStatus.Complete,
            priority: 2,
            linkedPr: 42,
            linkedIssues: ["issue1"],
            parentIssues: ["parent1"]);

        result.Description.Should().Be("A description");
        result.Status.Should().Be(IssueStatus.Complete);
        result.Priority.Should().Be(2);
        result.LinkedPR.Should().Be(42);
        result.LinkedIssues.Should().ContainSingle("issue1");
        result.ParentIssues.Should().ContainSingle("parent1");
    }

    [Test]
    public void CreateAsync_ThrowsOnNullTitle()
    {
        var act = async () => await _sut.CreateAsync(null!, IssueType.Task);

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task CreateAsync_RecordsChangeRecord()
    {
        _idGenerator.Generate(Arg.Any<string>()).Returns("abc123");

        await _sut.CreateAsync("Test Issue", IssueType.Task);

        await _changeService.Received(1).AddAsync(
            Arg.Is<ChangeRecord>(c => c.IssueId == "abc123" && c.Type == ChangeType.Created),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAllAsync_ReturnsAllIssues()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Test]
    public async Task GetByIdAsync_ReturnsIssue_WhenExists()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Test", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.GetByIdAsync("abc123");

        result.Should().NotBeNull();
        result!.Title.Should().Be("Test");
    }

    [Test]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());

        var result = await _sut.GetByIdAsync("nonexistent");

        result.Should().BeNull();
    }

    [Test]
    public async Task GetByIdAsync_IsCaseInsensitive()
    {
        var issues = new List<Issue>
        {
            new() { Id = "ABC123", Title = "Test", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.GetByIdAsync("abc123");

        result.Should().NotBeNull();
    }

    [Test]
    public async Task UpdateAsync_UpdatesProvidedFields()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Original", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.UpdateAsync("abc123", status: IssueStatus.Complete, priority: 1);

        result.Status.Should().Be(IssueStatus.Complete);
        result.Priority.Should().Be(1);
        result.Title.Should().Be("Original");
    }

    [Test]
    public async Task UpdateAsync_ThrowsWhenNotFound()
    {
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());

        var act = async () => await _sut.UpdateAsync("nonexistent", status: IssueStatus.Complete);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task UpdateAsync_RecordsChangeRecord()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Original", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        await _sut.UpdateAsync("abc123", status: IssueStatus.Complete);

        await _changeService.Received(1).AddAsync(
            Arg.Is<ChangeRecord>(c => c.IssueId == "abc123" && c.Type == ChangeType.Updated),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteAsync_ReturnsTrue_WhenDeleted()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Test", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.DeleteAsync("abc123");

        result.Should().BeTrue();
        await _storage.Received(1).SaveIssuesAsync(
            Arg.Is<IReadOnlyList<Issue>>(list => list.Count == 1 && list[0].Status == IssueStatus.Deleted),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());

        var result = await _sut.DeleteAsync("nonexistent");

        result.Should().BeFalse();
    }

    [Test]
    public async Task DeleteAsync_RecordsChangeRecord()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Test", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        await _sut.DeleteAsync("abc123");

        await _changeService.Received(1).AddAsync(
            Arg.Is<ChangeRecord>(c => c.IssueId == "abc123" && c.Type == ChangeType.Deleted),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SearchAsync_FindsMatchesInTitle()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "Fix login bug", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Add feature", Status = IssueStatus.Open, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.SearchAsync("login");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public async Task SearchAsync_FindsMatchesInDescription()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "Bug", Description = "Users cannot login", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Feature", Description = "Add new button", Status = IssueStatus.Open, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.SearchAsync("login");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public async Task FilterAsync_FiltersByStatus()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(status: IssueStatus.Open);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public async Task FilterAsync_FiltersByType()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(type: IssueType.Bug);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public async Task FilterAsync_CombinesFilters()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Bug, Priority = 1, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Bug, Priority = 2, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Complete, Type = IssueType.Bug, Priority = 1, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(status: IssueStatus.Open, type: IssueType.Bug, priority: 1);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }
}
