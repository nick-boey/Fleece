using Fleece.Core.Models;
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
    private ITagService _tagService = null!;
    private IssueService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _storage = Substitute.For<IStorageService>();
        _idGenerator = Substitute.For<IIdGenerator>();
        _gitConfigService = Substitute.For<IGitConfigService>();
        _tagService = new TagService();
        _gitConfigService.GetUserName().Returns("Test User");
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Issue>>(new List<Issue>()));
        _storage.LoadTombstonesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Tombstone>>([]));
        _sut = new IssueService(_storage, _idGenerator, _gitConfigService, _tagService);
    }

    [Test]
    public async Task CreateAsync_GeneratesId()
    {
        _idGenerator.Generate().Returns("abc123");

        var result = await _sut.CreateAsync("Test Issue", IssueType.Task);

        result.Id.Should().Be("abc123");
        _idGenerator.Received(1).Generate();
    }

    [Test]
    public async Task CreateAsync_AppendsThroughStorage()
    {
        _idGenerator.Generate().Returns("abc123");

        await _sut.CreateAsync("Test Issue", IssueType.Bug);

        await _storage.Received(1).AppendIssueAsync(
            Arg.Is<Issue>(i => i.Id == "abc123" && i.Title == "Test Issue" && i.Type == IssueType.Bug),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateAsync_SetsDefaultStatusToOpen()
    {
        _idGenerator.Generate().Returns("abc123");

        var result = await _sut.CreateAsync("Test Issue", IssueType.Task);

        result.Status.Should().Be(IssueStatus.Open);
    }

    [Test]
    public async Task CreateAsync_WithDraftStatus_SetsDraftStatus()
    {
        _idGenerator.Generate().Returns("abc123");

        var result = await _sut.CreateAsync("Test Issue", IssueType.Task, status: IssueStatus.Draft);

        result.Status.Should().Be(IssueStatus.Draft);
    }

    [Test]
    public async Task CreateAsync_SetsAllProvidedFields()
    {
        _idGenerator.Generate().Returns("abc123");

        var result = await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Feature,
            description: "A description",
            status: IssueStatus.Complete,
            priority: 2,
            linkedIssues: ["issue1"],
            parentIssues: [new ParentIssueRef { ParentIssue = "parent1", SortOrder = "aaa" }],
            tags: ["hsp-linked-pr=42"]);

        result.Description.Should().Be("A description");
        result.Status.Should().Be(IssueStatus.Complete);
        result.Priority.Should().Be(2);
        result.LinkedPRs.Should().ContainSingle().Which.Should().Be(42);
        result.LinkedIssues.Should().ContainSingle("issue1");
        result.ParentIssues.Should().ContainSingle().Which.ParentIssue.Should().Be("parent1");
    }

    [Test]
    public async Task CreateAsync_SetsTags()
    {
        _idGenerator.Generate().Returns("abc123");

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
        var result = await _sut.UpdateAsync("abc123", title: "New Title");

        result.Id.Should().Be("abc123");
        result.Title.Should().Be("New Title");
        _idGenerator.DidNotReceive().Generate();
    }

    [Test]
    public async Task UpdateAsync_ThrowsWhenNotFound()
    {
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());

        var act = async () => await _sut.UpdateAsync("nonexistent", status: IssueStatus.Complete);

        await act.Should().ThrowAsync<KeyNotFoundException>();
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
    public async Task CreateAsync_SetsWorkingBranchId()
    {
        _idGenerator.Generate().Returns("abc123");

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
        _idGenerator.Generate().Returns("abc123");

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
        _idGenerator.Generate().Returns("abc123");

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
        _idGenerator.Generate().Returns("abc123");

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
        _idGenerator.Generate().Returns("abc123");

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
        _idGenerator.Generate().Returns("abc123");

        var result = await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Task,
            workingBranchId: null);

        result.WorkingBranchId.Should().BeNull();
    }

    [Test]
    public async Task CreateAsync_RetriesOnTombstoneCollision()
    {
        var tombstones = new List<Tombstone>
        {
            new()
            {
                IssueId = "abc123",
                OriginalTitle = "Old issue",
                CleanedAt = DateTimeOffset.UtcNow,
                CleanedBy = "user"
            }
        };
        _storage.LoadTombstonesAsync(Arg.Any<CancellationToken>())
            .Returns(tombstones);

        _idGenerator.Generate().Returns("abc123", "def456");

        var result = await _sut.CreateAsync("Test Issue", IssueType.Task);

        result.Id.Should().Be("def456");
    }

    [Test]
    public async Task CreateAsync_ThrowsAfterMaxRetries()
    {
        // All generated IDs collide with tombstones
        var tombstones = new List<Tombstone>
        {
            new()
            {
                IssueId = "collid",
                OriginalTitle = "Old issue",
                CleanedAt = DateTimeOffset.UtcNow,
                CleanedBy = "user"
            }
        };

        _storage.LoadTombstonesAsync(Arg.Any<CancellationToken>())
            .Returns(tombstones);

        // All 11 calls return the same colliding ID
        _idGenerator.Generate().Returns("collid");

        var act = async () => await _sut.CreateAsync("Test Issue", IssueType.Task);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot generate a unique ID*");
    }

    [Test]
    public async Task CreateAsync_UsesFirstId_WhenNoCollision()
    {
        _storage.LoadTombstonesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Tombstone>
            {
                new()
                {
                    IssueId = "other1",
                    OriginalTitle = "Other issue",
                    CleanedAt = DateTimeOffset.UtcNow,
                    CleanedBy = "user"
                }
            });

        _idGenerator.Generate().Returns("abc123");

        var result = await _sut.CreateAsync("Test Issue", IssueType.Task);

        result.Id.Should().Be("abc123");
        _idGenerator.Received(1).Generate();
    }

    #region Tag Validation

    [Test]
    public void CreateAsync_ThrowsOnTagWithSpaces()
    {
        _idGenerator.Generate().Returns("abc123");

        var act = async () => await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Task,
            tags: ["bad tag"]);

        act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("tags");
    }

    [Test]
    public void CreateAsync_ThrowsOnTagWithReservedKey()
    {
        _idGenerator.Generate().Returns("abc123");

        var act = async () => await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Task,
            tags: ["status=open"]);

        act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("tags");
    }

    [Test]
    public async Task CreateAsync_AcceptsKeyValueTag()
    {
        _idGenerator.Generate().Returns("abc123");

        var result = await _sut.CreateAsync(
            title: "Test Issue",
            type: IssueType.Task,
            tags: ["project=frontend"]);

        result.Tags.Should().Contain("project=frontend");
    }

    [Test]
    public void UpdateAsync_ThrowsOnTagWithSpaces()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Original", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var act = async () => await _sut.UpdateAsync("abc123", tags: ["bad tag"]);

        act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("tags");
    }

    [Test]
    public void UpdateAsync_ThrowsOnTagWithReservedKey()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Original", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var act = async () => await _sut.UpdateAsync("abc123", tags: ["status=open"]);

        act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("tags");
    }

    #endregion


    #region GetIssueHierarchyAsync

    [Test]
    public async Task GetIssueHierarchyAsync_ReturnsEmpty_WhenIssueNotFound()
    {
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());

        var result = await _sut.GetIssueHierarchyAsync("nonexistent");

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetIssueHierarchyAsync_ReturnsSingleIssue_WhenNoParentsOrChildren()
    {
        var issues = new List<Issue>
        {
            new() { Id = "abc123", Title = "Standalone", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.GetIssueHierarchyAsync("abc123");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("abc123");
    }

    [Test]
    public async Task GetIssueHierarchyAsync_ReturnsIssueAndAllAncestors()
    {
        // Hierarchy: grandparent -> parent -> child
        var issues = new List<Issue>
        {
            new() { Id = "grandparent", Title = "Grandparent", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "parent", Title = "Parent", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "grandparent", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "child", Title = "Child", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "parent", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.GetIssueHierarchyAsync("child");

        result.Should().HaveCount(3);
        result.Select(i => i.Id).Should().BeEquivalentTo(["grandparent", "parent", "child"]);
    }

    [Test]
    public async Task GetIssueHierarchyAsync_ReturnsIssueAndAllDescendants()
    {
        // Hierarchy: root -> child1, root -> child2, child1 -> grandchild
        var issues = new List<Issue>
        {
            new() { Id = "root", Title = "Root", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "child1", Title = "Child1", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "root", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "child2", Title = "Child2", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "root", SortOrder = "b" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "grandchild", Title = "Grandchild", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "child1", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.GetIssueHierarchyAsync("root");

        result.Should().HaveCount(4);
        result.Select(i => i.Id).Should().BeEquivalentTo(["root", "child1", "child2", "grandchild"]);
    }

    [Test]
    public async Task GetIssueHierarchyAsync_ReturnsOnlyAncestors_WhenIncludeChildrenIsFalse()
    {
        // Hierarchy: grandparent -> parent -> child -> grandchild
        var issues = new List<Issue>
        {
            new() { Id = "grandparent", Title = "Grandparent", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "parent", Title = "Parent", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "grandparent", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "child", Title = "Child", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "parent", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "grandchild", Title = "Grandchild", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "child", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.GetIssueHierarchyAsync("child", includeParents: true, includeChildren: false);

        result.Should().HaveCount(3);
        result.Select(i => i.Id).Should().BeEquivalentTo(["grandparent", "parent", "child"]);
    }

    [Test]
    public async Task GetIssueHierarchyAsync_ReturnsOnlyDescendants_WhenIncludeParentsIsFalse()
    {
        // Hierarchy: grandparent -> parent -> child -> grandchild
        var issues = new List<Issue>
        {
            new() { Id = "grandparent", Title = "Grandparent", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "parent", Title = "Parent", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "grandparent", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "child", Title = "Child", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "parent", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "grandchild", Title = "Grandchild", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "child", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.GetIssueHierarchyAsync("parent", includeParents: false, includeChildren: true);

        result.Should().HaveCount(3);
        result.Select(i => i.Id).Should().BeEquivalentTo(["parent", "child", "grandchild"]);
    }

    [Test]
    public async Task GetIssueHierarchyAsync_HandlesDAG_MultipleParents()
    {
        // DAG: parent1 -> child, parent2 -> child (child has two parents)
        var issues = new List<Issue>
        {
            new() { Id = "parent1", Title = "Parent1", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "parent2", Title = "Parent2", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "child", Title = "Child", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [
                    new ParentIssueRef { ParentIssue = "parent1", SortOrder = "a" },
                    new ParentIssueRef { ParentIssue = "parent2", SortOrder = "b" }
                ], LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.GetIssueHierarchyAsync("child");

        result.Should().HaveCount(3);
        result.Select(i => i.Id).Should().BeEquivalentTo(["parent1", "parent2", "child"]);
    }

    [Test]
    public async Task GetIssueHierarchyAsync_ReturnsFullHierarchy_WhenBothDirectionsRequested()
    {
        // Hierarchy: grandparent -> parent -> targetIssue -> child -> grandchild
        var issues = new List<Issue>
        {
            new() { Id = "grandparent", Title = "Grandparent", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "parent", Title = "Parent", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "grandparent", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "target", Title = "Target", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "parent", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "child", Title = "Child", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "target", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "grandchild", Title = "Grandchild", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "child", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.GetIssueHierarchyAsync("target", includeParents: true, includeChildren: true);

        result.Should().HaveCount(5);
        result.Select(i => i.Id).Should().BeEquivalentTo(["grandparent", "parent", "target", "child", "grandchild"]);
    }

    [Test]
    public async Task GetIssueHierarchyAsync_ReturnsSingleIssue_WhenBothDirectionsDisabled()
    {
        // Hierarchy: parent -> targetIssue -> child
        var issues = new List<Issue>
        {
            new() { Id = "parent", Title = "Parent", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "target", Title = "Target", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "parent", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "child", Title = "Child", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "target", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.GetIssueHierarchyAsync("target", includeParents: false, includeChildren: false);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("target");
    }

    [Test]
    public async Task GetIssueHierarchyAsync_ExcludesUnrelatedIssues()
    {
        // target has parent and child, but there's also an unrelated issue
        var issues = new List<Issue>
        {
            new() { Id = "parent", Title = "Parent", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "target", Title = "Target", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "parent", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "child", Title = "Child", Status = IssueStatus.Open, Type = IssueType.Task,
                ParentIssues = [new ParentIssueRef { ParentIssue = "target", SortOrder = "a" }], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "unrelated", Title = "Unrelated", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };
        _storage.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.GetIssueHierarchyAsync("target");

        result.Should().HaveCount(3);
        result.Select(i => i.Id).Should().BeEquivalentTo(["parent", "target", "child"]);
        result.Select(i => i.Id).Should().NotContain("unrelated");
    }

    #endregion

    #region NormalizeSortOrders

    [Test]
    public void NormalizeSortOrders_AssignsSortOrder_WhenMissing()
    {
        var parentId = "parent1";
        var issues = new List<Issue>
        {
            CreateIssue("child-c", "Charlie", parentIssues:
            [
                new ParentIssueRef { ParentIssue = parentId, SortOrder = "" }
            ]),
            CreateIssue("child-a", "Alpha", parentIssues:
            [
                new ParentIssueRef { ParentIssue = parentId, SortOrder = "" }
            ]),
            CreateIssue("child-b", "Bravo", parentIssues:
            [
                new ParentIssueRef { ParentIssue = parentId, SortOrder = "" }
            ])
        };

        var result = IssueService.NormalizeSortOrders(issues);

        // All should have non-empty SortOrder
        foreach (var issue in result)
        {
            issue.ParentIssues.Should().AllSatisfy(p =>
                p.SortOrder.Should().NotBeNullOrEmpty());
        }

        // Alphabetical title order: Alpha < Bravo < Charlie
        var alpha = result.First(i => i.Id == "child-a").ParentIssues[0].SortOrder;
        var bravo = result.First(i => i.Id == "child-b").ParentIssues[0].SortOrder;
        var charlie = result.First(i => i.Id == "child-c").ParentIssues[0].SortOrder;

        string.Compare(alpha, bravo, StringComparison.Ordinal).Should().BeLessThan(0);
        string.Compare(bravo, charlie, StringComparison.Ordinal).Should().BeLessThan(0);
    }

    [Test]
    public void NormalizeSortOrders_PreservesExistingSortOrder()
    {
        var parentId = "parent1";
        var issues = new List<Issue>
        {
            CreateIssue("child-a", "Alpha", parentIssues:
            [
                new ParentIssueRef { ParentIssue = parentId, SortOrder = "zzz" }
            ]),
            CreateIssue("child-b", "Bravo", parentIssues:
            [
                new ParentIssueRef { ParentIssue = parentId, SortOrder = "mmm" }
            ])
        };

        var result = IssueService.NormalizeSortOrders(issues);

        result.First(i => i.Id == "child-a").ParentIssues[0].SortOrder.Should().Be("zzz");
        result.First(i => i.Id == "child-b").ParentIssues[0].SortOrder.Should().Be("mmm");
    }

    [Test]
    public void NormalizeSortOrders_ReturnsOriginalList_WhenNoNormalizationNeeded()
    {
        var issues = new List<Issue>
        {
            CreateIssue("child-a", "Alpha", parentIssues:
            [
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "aaa" }
            ])
        };

        var result = IssueService.NormalizeSortOrders(issues);

        result.Should().BeSameAs(issues);
    }

    [Test]
    public void NormalizeSortOrders_HandlesMultipleParents()
    {
        var issues = new List<Issue>
        {
            CreateIssue("child-a", "Alpha", parentIssues:
            [
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "" },
                new ParentIssueRef { ParentIssue = "parent2", SortOrder = "zzz" }
            ])
        };

        var result = IssueService.NormalizeSortOrders(issues);

        var parentRefs = result.First().ParentIssues;
        parentRefs[0].SortOrder.Should().NotBeNullOrEmpty(); // auto-assigned
        parentRefs[1].SortOrder.Should().Be("zzz"); // preserved
    }

    [Test]
    public void NormalizeSortOrders_StableAcrossMultipleCalls()
    {
        var parentId = "parent1";
        var issues = new List<Issue>
        {
            CreateIssue("child-b", "Bravo", parentIssues:
            [
                new ParentIssueRef { ParentIssue = parentId, SortOrder = "" }
            ]),
            CreateIssue("child-a", "Alpha", parentIssues:
            [
                new ParentIssueRef { ParentIssue = parentId, SortOrder = "" }
            ])
        };

        var result1 = IssueService.NormalizeSortOrders(issues);
        var result2 = IssueService.NormalizeSortOrders(issues);

        var alpha1 = result1.First(i => i.Id == "child-a").ParentIssues[0].SortOrder;
        var alpha2 = result2.First(i => i.Id == "child-a").ParentIssues[0].SortOrder;
        alpha1.Should().Be(alpha2);

        // Once assigned, a second normalization should be a no-op (returns same list)
        var result3 = IssueService.NormalizeSortOrders(result1);
        result3.Should().BeSameAs(result1);
    }

    [Test]
    public void NormalizeSortOrders_HandlesIssuesWithNoParents()
    {
        var issues = new List<Issue>
        {
            CreateIssue("orphan", "Orphan Issue")
        };

        var result = IssueService.NormalizeSortOrders(issues);

        result.Should().BeSameAs(issues);
    }

    private static Issue CreateIssue(
        string id,
        string title,
        IReadOnlyList<ParentIssueRef>? parentIssues = null,
        IssueStatus status = IssueStatus.Open,
        IssueType type = IssueType.Task)
    {
        return new Issue
        {
            Id = id,
            Title = title,
            Status = status,
            Type = type,
            ParentIssues = parentIssues ?? [],
            LastUpdate = DateTimeOffset.UtcNow
        };
    }

    #endregion
}
