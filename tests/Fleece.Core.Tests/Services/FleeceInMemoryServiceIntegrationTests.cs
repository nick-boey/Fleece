using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

/// <summary>
/// Integration tests for <see cref="FleeceInMemoryService"/> wired against real
/// <see cref="IssueService"/>, <see cref="JsonlStorageService"/>,
/// and <see cref="IssueSerializationQueueService"/>
/// — with actual JSONL files on disk.
///
/// Only <see cref="IIdGenerator"/> and <see cref="IGitConfigService"/> are mocked
/// to provide predictable IDs and avoid real git calls.
/// </summary>
[TestFixture]
public class FleeceInMemoryServiceIntegrationTests
{
    private string _tempDir = null!;
    private string _fleecePath = null!;

    // Real components
    private JsonlStorageService _storageService = null!;
    private IssueService _issueService = null!;
    private IssueSerializationQueueService _queueService = null!;
    private FleeceInMemoryService _sut = null!;

    // Mocks
    private IIdGenerator _idGenerator = null!;
    private IGitConfigService _gitConfigService = null!;

    private int _idCounter;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"fleece-integration-{Guid.NewGuid()}");
        _fleecePath = Path.Combine(_tempDir, ".fleece");
        Directory.CreateDirectory(_fleecePath);

        _idCounter = 0;

        _idGenerator = Substitute.For<IIdGenerator>();
        _idGenerator.Generate(Arg.Any<string>())
            .Returns(_ => $"test-{Interlocked.Increment(ref _idCounter)}");

        _gitConfigService = Substitute.For<IGitConfigService>();
        _gitConfigService.GetUserName().Returns("test-user");

        var serializer = new JsonlSerializer();
        var schemaValidator = new SchemaValidator();

        _storageService = new JsonlStorageService(_tempDir, serializer, schemaValidator);
        _issueService = new IssueService(_storageService, _idGenerator, _gitConfigService);

        _queueService = new IssueSerializationQueueService();
        _queueService.StartProcessing();

        _sut = new FleeceInMemoryService(_issueService, _queueService, _tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        _sut.Dispose();
        _queueService.Dispose();

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    #region Category A: Full CRUD Lifecycle

    [Test]
    public async Task Create_VerifyCacheHit_AndJsonlFileExistsOnDisk()
    {
        var issue = await _sut.CreateIssueAsync("My first issue", IssueType.Task, description: "A description");

        // Cache hit
        var cached = await _sut.GetIssueAsync(issue.Id);
        cached.Should().NotBeNull();
        cached!.Title.Should().Be("My first issue");
        cached.Description.Should().Be("A description");
        cached.Type.Should().Be(IssueType.Task);
        cached.Status.Should().Be(IssueStatus.Open);
        cached.CreatedBy.Should().Be("test-user");

        // Disk verification
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].Id.Should().Be(issue.Id);
        issuesOnDisk[0].Title.Should().Be("My first issue");
        issuesOnDisk[0].Description.Should().Be("A description");
    }

    [Test]
    public async Task Create_WithNonDefaultStatus_VerifiesBothCreateAndUpdatePersisted()
    {
        var issue = await _sut.CreateIssueAsync("Progress issue", IssueType.Bug, status: IssueStatus.Progress);

        issue.Status.Should().Be(IssueStatus.Progress);

        // Cache shows updated status
        var cached = await _sut.GetIssueAsync(issue.Id);
        cached!.Status.Should().Be(IssueStatus.Progress);

        // Disk shows updated status
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].Status.Should().Be(IssueStatus.Progress);
    }

    [Test]
    public async Task Update_Title_ReflectedInCacheAndDisk()
    {
        var created = await _sut.CreateIssueAsync("Original title", IssueType.Task);

        var updated = await _sut.UpdateIssueAsync(created.Id, title: "Updated title");
        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Updated title");

        // Cache reflects change
        var cached = await _sut.GetIssueAsync(created.Id);
        cached!.Title.Should().Be("Updated title");

        // Disk reflects change
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].Title.Should().Be("Updated title");
    }

    [Test]
    public async Task Update_Status_ReflectedInCacheAndDisk()
    {
        var created = await _sut.CreateIssueAsync("Status test", IssueType.Feature);

        var updated = await _sut.UpdateIssueAsync(created.Id, status: IssueStatus.Review);
        updated!.Status.Should().Be(IssueStatus.Review);

        // Cache reflects change
        var cached = await _sut.GetIssueAsync(created.Id);
        cached!.Status.Should().Be(IssueStatus.Review);

        // Disk reflects change
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk[0].Status.Should().Be(IssueStatus.Review);
    }

    [Test]
    public async Task Delete_SoftDeletes_VisibleInCacheWithDeletedStatus_AndDiskMatches()
    {
        var created = await _sut.CreateIssueAsync("To be deleted", IssueType.Chore);

        var deleted = await _sut.DeleteIssueAsync(created.Id);
        deleted.Should().BeTrue();

        // Cache shows soft-deleted status
        var cached = await _sut.GetIssueAsync(created.Id);
        cached.Should().NotBeNull();
        cached!.Status.Should().Be(IssueStatus.Deleted);

        // Disk matches
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].Status.Should().Be(IssueStatus.Deleted);
    }

    [Test]
    public async Task Delete_NonExistent_ReturnsFalse()
    {
        // Trigger initial cache load with empty store
        await _sut.ListIssuesAsync();

        var result = await _sut.DeleteIssueAsync("nonexistent-id");
        result.Should().BeFalse();
    }

    [Test]
    public async Task Create_MultipleIssues_AllVisibleInCacheAndDisk()
    {
        var issue1 = await _sut.CreateIssueAsync("Issue one", IssueType.Task);
        var issue2 = await _sut.CreateIssueAsync("Issue two", IssueType.Bug, description: "Bug description");
        var issue3 = await _sut.CreateIssueAsync("Issue three", IssueType.Feature, priority: 1);

        // All in cache
        var list = await _sut.ListIssuesAsync();
        list.Should().HaveCount(3);
        list.Select(i => i.Title).Should().BeEquivalentTo(["Issue one", "Issue two", "Issue three"]);

        // All on disk
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().HaveCount(3);
        issuesOnDisk.Select(i => i.Title).Should().BeEquivalentTo(["Issue one", "Issue two", "Issue three"]);
    }

    #endregion

    #region Category B: Cache-Disk Consistency

    [Test]
    public async Task ExternalDiskModification_ReloadAsync_CacheReflectsChange()
    {
        var created = await _sut.CreateIssueAsync("Original", IssueType.Task);

        // Modify disk directly via the underlying storage service (simulating external change)
        var issuesOnDisk = (await _storageService.LoadIssuesAsync()).ToList();
        var modified = issuesOnDisk[0] with { Title = "Externally Modified", LastUpdate = DateTimeOffset.UtcNow };
        await _storageService.SaveIssuesAsync([modified]);

        // Cache still shows old value
        var beforeReload = await _sut.GetIssueAsync(created.Id);
        beforeReload!.Title.Should().Be("Original");

        // After reload, cache reflects disk
        await _sut.ReloadAsync();
        var afterReload = await _sut.GetIssueAsync(created.Id);
        afterReload!.Title.Should().Be("Externally Modified");
    }

    [Test]
    public async Task NewServiceInstance_LoadsExistingDataFromDisk()
    {
        // Create data with first service instance
        await _sut.CreateIssueAsync("Existing issue", IssueType.Bug, description: "Already on disk");
        _sut.Dispose();

        // Stand up a new service instance against the same temp directory
        var newQueueService = new IssueSerializationQueueService();
        newQueueService.StartProcessing();
        using var newSut = new FleeceInMemoryService(_issueService, newQueueService, _tempDir);

        // The new instance should load the existing issue from disk
        var issues = await newSut.ListIssuesAsync();
        issues.Should().ContainSingle();
        issues[0].Title.Should().Be("Existing issue");
        issues[0].Description.Should().Be("Already on disk");

        newQueueService.Dispose();
    }

    [Test]
    public async Task ExternalIssueAppendedToDisk_Reload_VisibleInCache()
    {
        await _sut.CreateIssueAsync("First issue", IssueType.Task);

        // Externally add a second issue to disk
        var externalIssue = new Issue
        {
            Id = "ext-001",
            Title = "External issue",
            Status = IssueStatus.Open,
            Type = IssueType.Feature,
            LastUpdate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            TitleLastUpdate = DateTimeOffset.UtcNow,
            StatusLastUpdate = DateTimeOffset.UtcNow,
            TypeLastUpdate = DateTimeOffset.UtcNow,
            LinkedIssuesLastUpdate = DateTimeOffset.UtcNow,
            ParentIssuesLastUpdate = DateTimeOffset.UtcNow,
            TagsLastUpdate = DateTimeOffset.UtcNow
        };

        var existingIssues = (await _storageService.LoadIssuesAsync()).ToList();
        existingIssues.Add(externalIssue);
        await _storageService.SaveIssuesAsync(existingIssues);

        await _sut.ReloadAsync();

        var allIssues = await _sut.ListIssuesAsync();
        allIssues.Should().HaveCount(2);
        allIssues.Select(i => i.Id).Should().Contain("ext-001");
    }

    [Test]
    public async Task ExternalIssueRemovalFromDisk_Reload_RemovedFromCache()
    {
        var issue1 = await _sut.CreateIssueAsync("Issue to keep", IssueType.Task);
        var issue2 = await _sut.CreateIssueAsync("Issue to remove", IssueType.Bug);

        // Externally remove the second issue from disk
        var issuesOnDisk = (await _storageService.LoadIssuesAsync()).ToList();
        var remaining = issuesOnDisk.Where(i => i.Id == issue1.Id).ToList();
        await _storageService.SaveIssuesAsync(remaining);

        await _sut.ReloadAsync();

        var allIssues = await _sut.ListIssuesAsync();
        allIssues.Should().ContainSingle();
        allIssues[0].Id.Should().Be(issue1.Id);

        var removed = await _sut.GetIssueAsync(issue2.Id);
        removed.Should().BeNull();
    }

    #endregion

    #region Category C: FileSystemWatcher Integration

    [Test]
    public async Task FileWatcher_ExternalFileChange_AutoReloadsCache()
    {
        var created = await _sut.CreateIssueAsync("Watched issue", IssueType.Task);
        var cachedBefore = await _sut.GetIssueAsync(created.Id);
        cachedBefore!.Title.Should().Be("Watched issue");

        // Modify disk directly
        var issuesOnDisk = (await _storageService.LoadIssuesAsync()).ToList();
        var modified = issuesOnDisk[0] with { Title = "Watcher detected", LastUpdate = DateTimeOffset.UtcNow };
        await _storageService.SaveIssuesAsync([modified]);

        // Wait for debounce interval (500ms) + processing time
        await Task.Delay(1500);

        var cachedAfter = await _sut.GetIssueAsync(created.Id);
        cachedAfter!.Title.Should().Be("Watcher detected");
    }

    [Test]
    public async Task FileWatcher_RapidChanges_DebouncesAndReflectsFinalState()
    {
        var created = await _sut.CreateIssueAsync("Rapid changes test", IssueType.Task);

        // Make 5 rapid changes to disk
        for (var i = 0; i < 5; i++)
        {
            var issuesOnDisk = (await _storageService.LoadIssuesAsync()).ToList();
            var modified = issuesOnDisk[0] with
            {
                Title = $"Change {i}",
                LastUpdate = DateTimeOffset.UtcNow.AddMilliseconds(i)
            };
            await _storageService.SaveIssuesAsync([modified]);
            await Task.Delay(50); // Less than the 500ms debounce interval
        }

        // Wait for debounce + processing
        await Task.Delay(1500);

        var cachedAfter = await _sut.GetIssueAsync(created.Id);
        cachedAfter!.Title.Should().Be("Change 4"); // Final state
    }

    [Test]
    public async Task Service_WithoutFleeceDir_StillWorksCrud()
    {
        // Construct a service where .fleece dir doesn't exist at startup
        var emptyDir = Path.Combine(Path.GetTempPath(), $"fleece-nodir-{Guid.NewGuid()}");
        Directory.CreateDirectory(emptyDir);
        // NOTE: not creating .fleece subdirectory

        var serializer = new JsonlSerializer();
        var schemaValidator = new SchemaValidator();
        var storage = new JsonlStorageService(emptyDir, serializer, schemaValidator);
        var issueService = new IssueService(storage, _idGenerator, _gitConfigService);

        var queue = new IssueSerializationQueueService();
        queue.StartProcessing();

        using var sut = new FleeceInMemoryService(issueService, queue, emptyDir);

        // CRUD should still work (directory created lazily by storage)
        var issue = await sut.CreateIssueAsync("No dir test", IssueType.Task);
        issue.Title.Should().Be("No dir test");

        var cached = await sut.GetIssueAsync(issue.Id);
        cached.Should().NotBeNull();
        cached!.Title.Should().Be("No dir test");

        queue.Dispose();
        if (Directory.Exists(emptyDir))
        {
            Directory.Delete(emptyDir, true);
        }
    }

    #endregion

    #region Category D: Queue No-Op Verification

    /// <summary>
    /// Validates the finding that the serialization queue's WriteAction is a no-op.
    /// After a create, the disk already has the data (written by IssueService.CreateAsync),
    /// and the queue's WriteAction is <c>async _ => await Task.CompletedTask</c>.
    /// The queue drains to 0, but no additional disk writes occur.
    /// </summary>
    [Test]
    public async Task Queue_AfterCreate_DrainsToZero_DiskAlreadyWrittenByIssueService()
    {
        var issue = await _sut.CreateIssueAsync("Queue test", IssueType.Task);

        // Give the queue time to process the no-op write action
        await WaitForQueueDrainAsync();

        _queueService.PendingCount.Should().Be(0);

        // Disk already has the issue (written by IssueService, not by the queue)
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].Id.Should().Be(issue.Id);
    }

    /// <summary>
    /// Confirms that after the queue drains, there are no duplicate issues on disk.
    /// Since the WriteAction is a no-op, it shouldn't write a second copy.
    /// </summary>
    [Test]
    public async Task Queue_AfterDrain_NoDuplicateIssuesOnDisk()
    {
        await _sut.CreateIssueAsync("Issue A", IssueType.Task);
        await _sut.CreateIssueAsync("Issue B", IssueType.Bug);

        await WaitForQueueDrainAsync();

        _queueService.PendingCount.Should().Be(0);

        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().HaveCount(2);
        issuesOnDisk.Select(i => i.Title).Should().BeEquivalentTo(["Issue A", "Issue B"]);
    }

    /// <summary>
    /// Full lifecycle: create + update + delete. After all queue operations drain,
    /// the final disk state should reflect the cumulative result of all operations
    /// (all performed synchronously by IssueService, not by the queue).
    /// </summary>
    [Test]
    public async Task Queue_CreateUpdateDelete_AllDrain_FinalDiskStateCorrect()
    {
        var issue = await _sut.CreateIssueAsync("Lifecycle", IssueType.Task);
        await _sut.UpdateIssueAsync(issue.Id, title: "Updated Lifecycle");
        await _sut.DeleteIssueAsync(issue.Id);

        await WaitForQueueDrainAsync();

        _queueService.PendingCount.Should().Be(0);

        // Disk should show the final soft-deleted state
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].Title.Should().Be("Updated Lifecycle");
        issuesOnDisk[0].Status.Should().Be(IssueStatus.Deleted);
    }

    #endregion

    #region Category E: Read/Filter End-to-End

    [Test]
    public async Task ListIssuesAsync_ExcludesTerminalStatusesByDefault()
    {
        await _sut.CreateIssueAsync("Open issue", IssueType.Task);
        var toComplete = await _sut.CreateIssueAsync("Completed issue", IssueType.Task);
        await _sut.UpdateIssueAsync(toComplete.Id, status: IssueStatus.Complete);

        var result = await _sut.ListIssuesAsync();

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Open issue");
    }

    [Test]
    public async Task FilterAsync_CombinesStatusTypeAndPriority()
    {
        await _sut.CreateIssueAsync("Bug P1", IssueType.Bug, priority: 1, status: IssueStatus.Open);
        await _sut.CreateIssueAsync("Task P1", IssueType.Task, priority: 1, status: IssueStatus.Open);
        await _sut.CreateIssueAsync("Bug P3", IssueType.Bug, priority: 3, status: IssueStatus.Open);

        var result = await _sut.FilterAsync(status: IssueStatus.Open, type: IssueType.Bug, priority: 1);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Bug P1");
    }

    [Test]
    public async Task SearchAsync_MatchesTitleAndDescription()
    {
        await _sut.CreateIssueAsync("Login page bug", IssueType.Bug);
        await _sut.CreateIssueAsync("Dashboard feature", IssueType.Feature, description: "Includes login stats");
        await _sut.CreateIssueAsync("Unrelated task", IssueType.Task);

        var result = await _sut.SearchAsync("login");

        result.Should().HaveCount(2);
        result.Select(i => i.Title).Should().BeEquivalentTo(["Login page bug", "Dashboard feature"]);
    }

    [Test]
    public async Task FilterAsync_CombinesMultipleCriteria()
    {
        await _sut.CreateIssueAsync("Bug task", IssueType.Bug, priority: 1);
        await _sut.CreateIssueAsync("Feature task", IssueType.Feature, priority: 1);
        await _sut.CreateIssueAsync("Bug low priority", IssueType.Bug, priority: 5);

        var result = await _sut.FilterAsync(type: IssueType.Bug, priority: 1);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Bug task");
    }

    [Test]
    public async Task FilterAsync_IncludeTerminal_ReturnsDeletedIssues()
    {
        var issue = await _sut.CreateIssueAsync("To delete", IssueType.Task);
        await _sut.DeleteIssueAsync(issue.Id);

        // Without includeTerminal: excluded
        var withoutTerminal = await _sut.FilterAsync();
        withoutTerminal.Should().BeEmpty();

        // With includeTerminal: included
        var withTerminal = await _sut.FilterAsync(includeTerminal: true);
        withTerminal.Should().ContainSingle();
        withTerminal[0].Status.Should().Be(IssueStatus.Deleted);
    }

    #endregion

    #region Category F: Edge Cases

    [Test]
    public async Task GetIssueAsync_NonExistentId_ReturnsNull()
    {
        // Trigger cache load
        await _sut.ListIssuesAsync();

        var result = await _sut.GetIssueAsync("does-not-exist");
        result.Should().BeNull();
    }

    [Test]
    public async Task UpdateIssueAsync_NonExistentId_ReturnsNull()
    {
        // Trigger cache load
        await _sut.ListIssuesAsync();

        var result = await _sut.UpdateIssueAsync("does-not-exist", title: "New title");
        result.Should().BeNull();
    }

    [Test]
    public async Task EmptyStore_ListReturnsEmpty()
    {
        var result = await _sut.ListIssuesAsync();
        result.Should().BeEmpty();
    }

    [Test]
    public async Task IsLoaded_TransitionsFalseToTrueAfterFirstRead()
    {
        _sut.IsLoaded.Should().BeFalse();

        await _sut.ListIssuesAsync();

        _sut.IsLoaded.Should().BeTrue();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Waits for the serialization queue to drain all pending operations.
    /// Since all WriteActions are no-ops, this should complete almost instantly.
    /// </summary>
    private async Task WaitForQueueDrainAsync(int maxWaitMs = 2000)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(maxWaitMs);
        while (_queueService.PendingCount > 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    #endregion
}
