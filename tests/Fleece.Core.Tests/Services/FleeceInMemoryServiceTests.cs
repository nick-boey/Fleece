using Fleece.Core.Models;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class FleeceInMemoryServiceTests
{
    private IIssueService _issueService = null!;
    private IIssueSerializationQueue _serializationQueue = null!;
    private FleeceInMemoryService _sut = null!;
    private string _basePath = null!;

    [SetUp]
    public void SetUp()
    {
        _issueService = Substitute.For<IIssueService>();
        _serializationQueue = Substitute.For<IIssueSerializationQueue>();
        _basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_basePath);

        _sut = new FleeceInMemoryService(_issueService, _serializationQueue, _basePath);
    }

    [TearDown]
    public void TearDown()
    {
        _sut.Dispose();

        if (Directory.Exists(_basePath))
        {
            Directory.Delete(_basePath, true);
        }
    }

    #region Cache Loading

    [Test]
    public async Task GetIssueAsync_LoadsCacheOnFirstRead()
    {
        var issue = new IssueBuilder().WithId("issue1").WithTitle("Test").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.GetIssueAsync("issue1");

        result.Should().NotBeNull();
        result!.Id.Should().Be("issue1");
        await _issueService.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetIssueAsync_DoesNotReloadCacheOnSubsequentReads()
    {
        var issue = new IssueBuilder().WithId("issue1").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        await _sut.GetIssueAsync("issue1");
        await _sut.GetIssueAsync("issue1");

        await _issueService.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task IsLoaded_IsFalseBeforeFirstRead_TrueAfter()
    {
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());

        _sut.IsLoaded.Should().BeFalse();
        await _sut.GetIssueAsync("nonexistent");
        _sut.IsLoaded.Should().BeTrue();
    }

    [Test]
    public async Task ReloadAsync_ReloadsFromDisk()
    {
        var issue1 = new IssueBuilder().WithId("issue1").WithTitle("Original").Build();
        var issue2 = new IssueBuilder().WithId("issue1").WithTitle("Updated").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([issue1], [issue2]);

        await _sut.GetIssueAsync("issue1");
        var beforeReload = await _sut.GetIssueAsync("issue1");
        beforeReload!.Title.Should().Be("Original");

        await _sut.ReloadAsync();
        var afterReload = await _sut.GetIssueAsync("issue1");
        afterReload!.Title.Should().Be("Updated");
    }

    #endregion

    #region File Watching

    [Test]
    public async Task FileWatcher_ReloadsCacheOnExternalFileChange()
    {
        // Arrange: create .fleece directory and construct a NEW service so the watcher is active
        var watchBasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var fleecePath = Path.Combine(watchBasePath, ".fleece");
        Directory.CreateDirectory(fleecePath);
        File.WriteAllText(Path.Combine(fleecePath, "issues.jsonl"), "");

        var issueService = Substitute.For<IIssueService>();
        var serializationQueue = Substitute.For<IIssueSerializationQueue>();

        var issueV1 = new IssueBuilder().WithId("issue1").WithTitle("Original").Build();
        var issueV2 = new IssueBuilder().WithId("issue1").WithTitle("ExternallyUpdated").Build();

        var callCount = 0;
        issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return callCount == 1
                    ? Task.FromResult<IReadOnlyList<Issue>>([issueV1])
                    : Task.FromResult<IReadOnlyList<Issue>>([issueV2]);
            });

        using var sut = new FleeceInMemoryService(issueService, serializationQueue, watchBasePath);

        // Act: trigger initial cache load
        var initial = await sut.GetIssueAsync("issue1");
        initial!.Title.Should().Be("Original");

        // Simulate an external file change to trigger the FileSystemWatcher
        await File.WriteAllTextAsync(Path.Combine(fleecePath, "issues.jsonl"), "changed");

        // Wait for debounce interval (500ms) + processing time
        await Task.Delay(1500);

        // Assert: cache should have been reloaded with the updated issue
        var reloaded = await sut.GetIssueAsync("issue1");
        reloaded!.Title.Should().Be("ExternallyUpdated");

        // Cleanup
        sut.Dispose();
        if (Directory.Exists(watchBasePath))
        {
            Directory.Delete(watchBasePath, true);
        }
    }

    [Test]
    public async Task FileWatcher_DebouncesRapidChanges()
    {
        // Arrange: create .fleece directory so watcher is active
        var watchBasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var fleecePath = Path.Combine(watchBasePath, ".fleece");
        Directory.CreateDirectory(fleecePath);
        File.WriteAllText(Path.Combine(fleecePath, "issues.jsonl"), "");

        var issueService = Substitute.For<IIssueService>();
        var serializationQueue = Substitute.For<IIssueSerializationQueue>();

        var getAllCallCount = 0;
        var issue = new IssueBuilder().WithId("issue1").WithTitle("Test").Build();
        issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref getAllCallCount);
                return Task.FromResult<IReadOnlyList<Issue>>([issue]);
            });

        using var sut = new FleeceInMemoryService(issueService, serializationQueue, watchBasePath);

        // Trigger initial load
        await sut.GetIssueAsync("issue1");
        getAllCallCount.Should().Be(1);

        // Act: trigger multiple rapid file changes
        for (var i = 0; i < 5; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(fleecePath, "issues.jsonl"), $"change-{i}");
            await Task.Delay(50); // Less than the 500ms debounce interval
        }

        // Wait for debounce interval + processing
        await Task.Delay(1500);

        // Assert: GetAllAsync should have been called at most a few times (initial + debounced reload)
        // Not 6 times (1 initial + 5 for each file change)
        // Initial load = 1, debounced reload(s) should add only 1-2 more
        getAllCallCount.Should().BeLessThanOrEqualTo(4);

        // Cleanup
        sut.Dispose();
        if (Directory.Exists(watchBasePath))
        {
            Directory.Delete(watchBasePath, true);
        }
    }

    #endregion

    #region Read Operations

    [Test]
    public async Task GetIssueAsync_ReturnsNullForMissingIssue()
    {
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());

        var result = await _sut.GetIssueAsync("nonexistent");

        result.Should().BeNull();
    }

    [Test]
    public async Task GetIssueAsync_IsCaseInsensitive()
    {
        var issue = new IssueBuilder().WithId("AbCdEf").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.GetIssueAsync("abcdef");

        result.Should().NotBeNull();
        result!.Id.Should().Be("AbCdEf");
    }

    [Test]
    public async Task ListIssuesAsync_ReturnsAllNonTerminalIssuesWhenNoFilters()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build(),
            new IssueBuilder().WithId("prog1").WithStatus(IssueStatus.Progress).Build(),
            new IssueBuilder().WithId("comp1").WithStatus(IssueStatus.Complete).Build(),
            new IssueBuilder().WithId("arch1").WithStatus(IssueStatus.Archived).Build(),
            new IssueBuilder().WithId("del1").WithStatus(IssueStatus.Deleted).Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.ListIssuesAsync();

        result.Should().HaveCount(2);
        result.Select(i => i.Id).Should().BeEquivalentTo(["open1", "prog1"]);
    }

    [Test]
    public async Task ListIssuesAsync_FiltersByStatus()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build(),
            new IssueBuilder().WithId("comp1").WithStatus(IssueStatus.Complete).Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.ListIssuesAsync(status: IssueStatus.Complete);

        result.Should().ContainSingle().Which.Id.Should().Be("comp1");
    }

    [Test]
    public async Task ListIssuesAsync_FiltersByType()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("task1").WithType(IssueType.Task).Build(),
            new IssueBuilder().WithId("bug1").WithType(IssueType.Bug).Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.ListIssuesAsync(type: IssueType.Bug);

        result.Should().ContainSingle().Which.Id.Should().Be("bug1");
    }

    [Test]
    public async Task ListIssuesAsync_FiltersByPriority()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("p1").WithPriority(1).Build(),
            new IssueBuilder().WithId("p3").WithPriority(3).Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.ListIssuesAsync(priority: 3);

        result.Should().ContainSingle().Which.Id.Should().Be("p3");
    }

    [Test]
    public async Task SearchAsync_ReturnsEmptyForNullOrWhitespaceQuery()
    {
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());

        var result = await _sut.SearchAsync("");

        result.Should().BeEmpty();
    }

    [Test]
    public async Task SearchAsync_MatchesTitleDescriptionAndTags()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("t1").WithTitle("Fix login bug").Build(),
            new IssueBuilder().WithId("t2").WithTitle("Add feature").WithDescription("login related").Build(),
            new IssueBuilder().WithId("t3").WithTitle("Unrelated").WithTags("login").Build(),
            new IssueBuilder().WithId("t4").WithTitle("Nothing here").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.SearchAsync("login");

        result.Should().HaveCount(3);
        result.Select(i => i.Id).Should().BeEquivalentTo(["t1", "t2", "t3"]);
    }

    [Test]
    public async Task FilterAsync_CombinesMultipleCriteria()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("match").WithStatus(IssueStatus.Open).WithType(IssueType.Bug).WithPriority(1).Build(),
            new IssueBuilder().WithId("wrong-type").WithStatus(IssueStatus.Open).WithType(IssueType.Task).WithPriority(1).Build(),
            new IssueBuilder().WithId("wrong-priority").WithStatus(IssueStatus.Open).WithType(IssueType.Bug).WithPriority(3).Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(status: IssueStatus.Open, type: IssueType.Bug, priority: 1);

        result.Should().ContainSingle().Which.Id.Should().Be("match");
    }

    [Test]
    public async Task FilterAsync_ExcludesTerminalByDefault()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build(),
            new IssueBuilder().WithId("closed1").WithStatus(IssueStatus.Closed).Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync();

        result.Should().ContainSingle().Which.Id.Should().Be("open1");
    }

    [Test]
    public async Task FilterAsync_IncludesTerminalWhenRequested()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build(),
            new IssueBuilder().WithId("closed1").WithStatus(IssueStatus.Closed).Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(includeTerminal: true);

        result.Should().HaveCount(2);
    }

    [Test]
    public async Task FilterAsync_FiltersByAssignedTo()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("a1").WithAssignedTo("alice").Build(),
            new IssueBuilder().WithId("a2").WithAssignedTo("bob").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(assignedTo: "alice");

        result.Should().ContainSingle().Which.Id.Should().Be("a1");
    }

    [Test]
    public async Task FilterAsync_FiltersByTags()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("tagged").WithTags("urgent").Build(),
            new IssueBuilder().WithId("untagged").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(tags: new List<string> { "urgent" });

        result.Should().ContainSingle().Which.Id.Should().Be("tagged");
    }

    [Test]
    public async Task FilterAsync_FiltersByLinkedPr()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("linked").WithLinkedPr(42).Build(),
            new IssueBuilder().WithId("unlinked").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.FilterAsync(linkedPr: 42);

        result.Should().ContainSingle().Which.Id.Should().Be("linked");
    }

    #endregion

    #region Write Operations

    [Test]
    public async Task CreateIssueAsync_UpdatesCacheAndEnqueuesWrite()
    {
        var issue = new IssueBuilder().WithId("new1").WithTitle("New Issue").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());
        _issueService.CreateAsync(
            Arg.Any<string>(), Arg.Any<IssueType>(),
            Arg.Any<string?>(), Arg.Any<IssueStatus>(),
            Arg.Any<int?>(), Arg.Any<int?>(),
            Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<ParentIssueRef>?>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<string?>(), Arg.Any<ExecutionMode?>(),
            Arg.Any<CancellationToken>())
            .Returns(issue);

        var result = await _sut.CreateIssueAsync("New Issue", IssueType.Task);

        result.Id.Should().Be("new1");

        // Verify cache was updated
        var cached = await _sut.GetIssueAsync("new1");
        cached.Should().NotBeNull();

        // Verify write was enqueued
        await _serializationQueue.Received(1).EnqueueAsync(
            Arg.Is<IssueWriteOperation>(op => op.IssueId == "new1" && op.Type == WriteOperationType.Create),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateIssueAsync_WithExplicitStatus_PassesStatusToCreate()
    {
        var created = new IssueBuilder().WithId("new1").WithStatus(IssueStatus.Progress).Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());
        _issueService.CreateAsync(
            Arg.Any<string>(), Arg.Any<IssueType>(),
            Arg.Any<string?>(), IssueStatus.Progress,
            Arg.Any<int?>(), Arg.Any<int?>(),
            Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<ParentIssueRef>?>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<string?>(), Arg.Any<ExecutionMode?>(),
            Arg.Any<CancellationToken>())
            .Returns(created);

        var result = await _sut.CreateIssueAsync("New Issue", IssueType.Task, status: IssueStatus.Progress);

        result.Status.Should().Be(IssueStatus.Progress);
    }

    [Test]
    public async Task UpdateIssueAsync_UpdatesCacheAndEnqueuesWrite()
    {
        var original = new IssueBuilder().WithId("issue1").WithTitle("Original").Build();
        var updated = new IssueBuilder().WithId("issue1").WithTitle("Updated").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([original]);
        _issueService.UpdateAsync(
            "issue1",
            "Updated", Arg.Any<string?>(),
            Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(),
            Arg.Any<int?>(), Arg.Any<int?>(),
            Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<ParentIssueRef>?>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<string?>(), Arg.Any<ExecutionMode?>(),
            Arg.Any<CancellationToken>())
            .Returns(updated);

        var result = await _sut.UpdateIssueAsync("issue1", title: "Updated");

        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated");

        // Verify cache was updated
        var cached = await _sut.GetIssueAsync("issue1");
        cached!.Title.Should().Be("Updated");

        // Verify write was enqueued
        await _serializationQueue.Received(1).EnqueueAsync(
            Arg.Is<IssueWriteOperation>(op => op.IssueId == "issue1" && op.Type == WriteOperationType.Update),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateIssueAsync_ReturnsNullForMissingIssue()
    {
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());

        var result = await _sut.UpdateIssueAsync("nonexistent", title: "Updated");

        result.Should().BeNull();
    }

    [Test]
    public async Task UpdateIssueAsync_HandlesKeyNotFoundException()
    {
        var original = new IssueBuilder().WithId("issue1").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([original]);
        _issueService.UpdateAsync(
            "issue1",
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(),
            Arg.Any<int?>(), Arg.Any<int?>(),
            Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<ParentIssueRef>?>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<string?>(), Arg.Any<ExecutionMode?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new KeyNotFoundException("not found"));

        var result = await _sut.UpdateIssueAsync("issue1", title: "Updated");

        result.Should().BeNull();

        // Issue should be removed from cache
        var cached = await _sut.GetIssueAsync("issue1");
        cached.Should().BeNull();
    }

    [Test]
    public async Task DeleteIssueAsync_UpdatesCacheAndEnqueuesWrite()
    {
        var original = new IssueBuilder().WithId("issue1").Build();
        var deleted = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Deleted).Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([original]);
        _issueService.DeleteAsync("issue1", Arg.Any<CancellationToken>()).Returns(true);
        _issueService.GetByIdAsync("issue1", Arg.Any<CancellationToken>()).Returns(deleted);

        var result = await _sut.DeleteIssueAsync("issue1");

        result.Should().BeTrue();

        // Cache should have the soft-deleted version
        var cached = await _sut.GetIssueAsync("issue1");
        cached.Should().NotBeNull();
        cached!.Status.Should().Be(IssueStatus.Deleted);

        // Verify write was enqueued
        await _serializationQueue.Received(1).EnqueueAsync(
            Arg.Is<IssueWriteOperation>(op => op.IssueId == "issue1" && op.Type == WriteOperationType.Delete),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteIssueAsync_ReturnsFalseForMissingIssue()
    {
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());
        _issueService.DeleteAsync("nonexistent", Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.DeleteIssueAsync("nonexistent");

        result.Should().BeFalse();
    }

    [Test]
    public async Task DeleteIssueAsync_RemovesFromCacheWhenGetByIdReturnsNull()
    {
        var original = new IssueBuilder().WithId("issue1").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([original]);
        _issueService.DeleteAsync("issue1", Arg.Any<CancellationToken>()).Returns(true);
        _issueService.GetByIdAsync("issue1", Arg.Any<CancellationToken>()).Returns((Issue?)null);

        await _sut.DeleteIssueAsync("issue1");

        var cached = await _sut.GetIssueAsync("issue1");
        cached.Should().BeNull();
    }

    #endregion

    #region Disposed Service

    [Test]
    public void GetIssueAsync_ThrowsWhenDisposed()
    {
        _sut.Dispose();

        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _sut.GetIssueAsync("issue1"));
    }

    [Test]
    public void ListIssuesAsync_ThrowsWhenDisposed()
    {
        _sut.Dispose();

        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _sut.ListIssuesAsync());
    }

    [Test]
    public void SearchAsync_ThrowsWhenDisposed()
    {
        _sut.Dispose();

        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _sut.SearchAsync("query"));
    }

    [Test]
    public void FilterAsync_ThrowsWhenDisposed()
    {
        _sut.Dispose();

        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _sut.FilterAsync());
    }

    [Test]
    public void CreateIssueAsync_ThrowsWhenDisposed()
    {
        _sut.Dispose();

        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _sut.CreateIssueAsync("title", IssueType.Task));
    }

    [Test]
    public void UpdateIssueAsync_ThrowsWhenDisposed()
    {
        _sut.Dispose();

        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _sut.UpdateIssueAsync("issue1", title: "Updated"));
    }

    [Test]
    public void DeleteIssueAsync_ThrowsWhenDisposed()
    {
        _sut.Dispose();

        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _sut.DeleteIssueAsync("issue1"));
    }

    [Test]
    public void ReloadAsync_ThrowsWhenDisposed()
    {
        _sut.Dispose();

        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _sut.ReloadAsync());
    }

    [Test]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _sut.Dispose();

        Assert.DoesNotThrow(() => _sut.Dispose());
    }

    #endregion
}
