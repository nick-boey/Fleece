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
            parentIssues: [new ParentIssueRef { ParentIssue = "parent1", SortOrder = "aaa" }]);

        result.Description.Should().Be("A description");
        result.Status.Should().Be(IssueStatus.Complete);
        result.Priority.Should().Be(2);
        result.LinkedPR.Should().Be(42);
        result.LinkedIssues.Should().ContainSingle("issue1");
        result.ParentIssues.Should().ContainSingle().Which.ParentIssue.Should().Be("parent1");
    }

    [Test]
    public async Task CreateAsync_SetsTags()
    {
        _idGenerator.Generate(Arg.Any<string>()).Returns("abc123");

        var result = await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Task,
            tags: ["backend", "api", "urgent"]);

        result.Tags.Should().HaveCount(3);
        result.Tags.Should().Contain(["backend", "api", "urgent"]);
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
    public async Task ResolveByPartialIdAsync_ReturnsSingleIssue_WhenPartialIdMatchesOne()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Test", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "def456", Title = "Other", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.ResolveByPartialIdAsync("abc");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("abc123");
    }

    [Test]
    public async Task ResolveByPartialIdAsync_ReturnsMultipleIssues_WhenPartialIdMatchesMany()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Test 1", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "abc456", Title = "Test 2", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "def789", Title = "Other", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.ResolveByPartialIdAsync("abc");

        result.Should().HaveCount(2);
        result.Select(i => i.Id).Should().BeEquivalentTo(["abc123", "abc456"]);
    }

    [Test]
    public async Task ResolveByPartialIdAsync_ReturnsEmpty_WhenNoMatches()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Test", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.ResolveByPartialIdAsync("xyz");

        result.Should().BeEmpty();
    }

    [Test]
    public async Task ResolveByPartialIdAsync_RequiresExactMatch_WhenLessThanThreeCharacters()
    {
        var issues = new List<Issue>
        {
            new() { Id = "ab", Title = "Exact", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "abc123", Title = "Partial", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.ResolveByPartialIdAsync("ab");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("ab");
    }

    [Test]
    public async Task ResolveByPartialIdAsync_IsCaseInsensitive()
    {
        var issues = new List<Issue>
        {
            new() { Id = "ABC123", Title = "Test", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.ResolveByPartialIdAsync("abc");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("ABC123");
    }

    [Test]
    public async Task ResolveByPartialIdAsync_ReturnsEmpty_WhenPartialIdIsEmpty()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Test", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.ResolveByPartialIdAsync("");

        result.Should().BeEmpty();
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
    public async Task UpdateAsync_PreservesOriginalId_WhenTitleChanges()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Original Title", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);
        _idGenerator.Generate("New Title").Returns("different_id");

        var result = await _sut.UpdateAsync("abc123", title: "New Title");

        result.Id.Should().Be("abc123");
        result.Title.Should().Be("New Title");
        _idGenerator.DidNotReceive().Generate(Arg.Any<string>());
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

    [Test]
    public async Task FilterAsync_FiltersBySingleTag()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["backend", "api"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["frontend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = [], LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(tags: ["backend"]);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public async Task FilterAsync_FiltersByMultipleTags_OrLogic()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["backend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["frontend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["docs"], LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(tags: ["backend", "frontend"]);

        result.Should().HaveCount(2);
        result.Select(i => i.Id).Should().Contain(["a", "b"]);
    }

    [Test]
    public async Task FilterAsync_FiltersByLinkedPr()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, LinkedPR = 123, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, LinkedPR = 456, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Progress, Type = IssueType.Task, LinkedPR = null, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(linkedPr: 123);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public async Task FilterAsync_TagFilterIsCaseInsensitive()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["Backend", "API"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["frontend"], LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(tags: ["backend"]);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public async Task SearchAsync_FindsMatchesInTags()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "Fix bug", Tags = ["backend", "api"], Status = IssueStatus.Progress, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Add feature", Tags = ["frontend"], Status = IssueStatus.Progress, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.SearchAsync("backend");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public async Task CreateAsync_SetsWorkingBranchId()
    {
        _idGenerator.Generate(Arg.Any<string>()).Returns("abc123");

        var result = await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Task,
            workingBranchId: "feat/my-feature");

        result.WorkingBranchId.Should().Be("feat/my-feature");
    }

    [Test]
    public async Task UpdateAsync_UpdatesWorkingBranchId()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Original", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.UpdateAsync("abc123", workingBranchId: "fix/bug-123");

        result.WorkingBranchId.Should().Be("fix/bug-123");
    }

    [Test]
    public void CreateAsync_ThrowsOnInvalidBranchName_WithSpace()
    {
        _idGenerator.Generate(Arg.Any<string>()).Returns("abc123");

        var act = async () => await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Task,
            workingBranchId: "invalid branch name");

        act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("workingBranchId");
    }

    [Test]
    public void CreateAsync_ThrowsOnInvalidBranchName_WithTilde()
    {
        _idGenerator.Generate(Arg.Any<string>()).Returns("abc123");

        var act = async () => await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Task,
            workingBranchId: "invalid~branch");

        act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("workingBranchId");
    }

    [Test]
    public void UpdateAsync_ThrowsOnInvalidBranchName()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Original", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var act = async () => await _sut.UpdateAsync("abc123", workingBranchId: "invalid:branch");

        act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("workingBranchId");
    }

    [TestCase("feat/my-feature")]
    [TestCase("fix/bug-123")]
    [TestCase("feature/add-login")]
    [TestCase("my-branch")]
    [TestCase("my_branch")]
    [TestCase("v1.0.0")]
    [TestCase("a")]
    public async Task CreateAsync_AcceptsValidBranchNames(string branchName)
    {
        _idGenerator.Generate(Arg.Any<string>()).Returns("abc123");

        var result = await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Task,
            workingBranchId: branchName);

        result.WorkingBranchId.Should().Be(branchName);
    }

    [TestCase("invalid branch")]
    [TestCase("invalid~branch")]
    [TestCase("invalid^branch")]
    [TestCase("invalid:branch")]
    [TestCase("invalid?branch")]
    [TestCase("invalid*branch")]
    [TestCase("invalid[branch")]
    [TestCase(".hidden")]
    [TestCase("branch.lock")]
    [TestCase("branch..name")]
    [TestCase("/leading-slash")]
    [TestCase("trailing-slash/")]
    public void CreateAsync_RejectsInvalidBranchNames(string branchName)
    {
        _idGenerator.Generate(Arg.Any<string>()).Returns("abc123");

        var act = async () => await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Task,
            workingBranchId: branchName);

        act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("workingBranchId");
    }

    [Test]
    public async Task CreateAsync_AllowsNullWorkingBranchId()
    {
        _idGenerator.Generate(Arg.Any<string>()).Returns("abc123");

        var result = await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Task,
            workingBranchId: null);

        result.WorkingBranchId.Should().BeNull();
    }

    [Test]
    public async Task FilterAsync_ExcludesTerminalStatuses_ByDefault()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "d", Title = "D", Status = IssueStatus.Progress, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "e", Title = "E", Status = IssueStatus.Review, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "f", Title = "F", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "g", Title = "G", Status = IssueStatus.Archived, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "h", Title = "H", Status = IssueStatus.Closed, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "i", Title = "I", Status = IssueStatus.Deleted, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync();

        result.Should().HaveCount(5);
        result.Select(i => i.Id).Should().Contain(["a", "b", "c", "d", "e"]);
        result.Select(i => i.Id).Should().NotContain(["f", "g", "h", "i"]);
    }

    [Test]
    public async Task FilterAsync_IncludesTerminalStatuses_WhenIncludeTerminalIsTrue()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Archived, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "d", Title = "D", Status = IssueStatus.Closed, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "e", Title = "E", Status = IssueStatus.Deleted, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(includeTerminal: true);

        result.Should().HaveCount(5);
        result.Select(i => i.Id).Should().Contain(["a", "b", "c", "d", "e"]);
    }

    [Test]
    public async Task FilterAsync_IncludesTerminalStatus_WhenExplicitlyFiltered()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // When a specific terminal status is requested, it should be returned even without includeTerminal
        var result = await _sut.FilterAsync(status: IssueStatus.Complete);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("b");
    }

    [Test]
    public async Task FilterAsync_ExcludesAllTerminalStatuses_Complete()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task FilterAsync_ExcludesAllTerminalStatuses_Archived()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Archived, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task FilterAsync_ExcludesAllTerminalStatuses_Closed()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Closed, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task FilterAsync_ExcludesAllTerminalStatuses_Deleted()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Deleted, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task FilterAsync_CombinesTerminalFilterWithOtherFilters()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Bug, Priority = 1, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Priority = 1, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Complete, Type = IssueType.Bug, Priority = 1, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(type: IssueType.Bug, priority: 1);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }
}
