using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

/// <summary>
/// Integration tests for the clean/tombstone workflow with real JSONL files on disk.
///
/// Covers the manual test plan items from the PR:
///   - <c>fleece delete</c> then <c>fleece clean --dry-run</c> shows the issue
///   - <c>fleece clean</c> removes the issue and creates tombstone file
///   - <c>fleece create -t "&lt;same title&gt;"</c> generates a different (salted) ID
///   - <c>fleece merge</c> consolidates tombstone files
///
/// Only <see cref="IGitConfigService"/> is mocked to avoid real git calls.
/// Real <see cref="Sha256IdGenerator"/> is used to verify salted ID generation.
/// </summary>
[TestFixture]
public class CleanIntegrationTests
{
    private string _tempDir = null!;
    private string _fleecePath = null!;

    // Real components
    private JsonlStorageService _storageService = null!;
    private IssueService _issueService = null!;
    private ChangeService _changeService = null!;
    private CleanService _cleanService = null!;
    private MergeService _mergeService = null!;
    private Sha256IdGenerator _idGenerator = null!;

    // Mocks
    private IGitConfigService _gitConfigService = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"fleece-clean-integration-{Guid.NewGuid()}");
        _fleecePath = Path.Combine(_tempDir, ".fleece");
        Directory.CreateDirectory(_fleecePath);

        _gitConfigService = Substitute.For<IGitConfigService>();
        _gitConfigService.GetUserName().Returns("test-user");

        var serializer = new JsonlSerializer();
        var schemaValidator = new SchemaValidator();

        _idGenerator = new Sha256IdGenerator();
        _storageService = new JsonlStorageService(_tempDir, serializer, schemaValidator);
        _changeService = new ChangeService(_storageService);
        _issueService = new IssueService(_storageService, _idGenerator, _gitConfigService, _changeService);
        _cleanService = new CleanService(_storageService, _gitConfigService);
        _mergeService = new MergeService(_storageService, _gitConfigService, serializer);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    #region Delete then Clean --dry-run

    [Test]
    public async Task DryRun_AfterDelete_ShowsDeletedIssueWithoutModifyingDisk()
    {
        // Arrange: create an issue and soft-delete it
        var issue = await _issueService.CreateAsync("Issue to clean", IssueType.Task);
        await _issueService.DeleteAsync(issue.Id);

        // Act: clean with dry-run
        var result = await _cleanService.CleanAsync(dryRun: true);

        // Assert: result shows the deleted issue
        result.CleanedTombstones.Should().HaveCount(1);
        result.CleanedTombstones[0].IssueId.Should().Be(issue.Id);
        result.CleanedTombstones[0].OriginalTitle.Should().Be("Issue to clean");

        // Assert: disk still has the original issue (not cleaned)
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].Id.Should().Be(issue.Id);
        issuesOnDisk[0].Status.Should().Be(IssueStatus.Deleted);

        // Assert: no tombstone file created
        var tombstones = await _storageService.LoadTombstonesAsync();
        tombstones.Should().BeEmpty();
    }

    [Test]
    public async Task DryRun_WithMultipleIssues_OnlyShowsDeletedOnes()
    {
        // Arrange: create issues with various statuses
        var openIssue = await _issueService.CreateAsync("Open issue", IssueType.Task);
        var deletedIssue = await _issueService.CreateAsync("Deleted issue", IssueType.Bug);
        var completedIssue = await _issueService.CreateAsync("Completed issue", IssueType.Feature);
        await _issueService.DeleteAsync(deletedIssue.Id);
        await _issueService.UpdateAsync(completedIssue.Id, status: IssueStatus.Complete);

        // Act: dry-run clean (default: only Deleted status)
        var result = await _cleanService.CleanAsync(dryRun: true);

        // Assert: only the deleted issue shows up
        result.CleanedTombstones.Should().HaveCount(1);
        result.CleanedTombstones[0].IssueId.Should().Be(deletedIssue.Id);

        // Assert: all 3 issues still on disk
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().HaveCount(3);
    }

    #endregion

    #region Clean removes issue and creates tombstone file

    [Test]
    public async Task Clean_RemovesDeletedIssue_AndCreatesTombstoneFile()
    {
        // Arrange: create and delete an issue
        var issue = await _issueService.CreateAsync("Doomed issue", IssueType.Bug);
        await _issueService.DeleteAsync(issue.Id);

        // Act
        var result = await _cleanService.CleanAsync();

        // Assert: result reports the cleaned issue
        result.CleanedTombstones.Should().HaveCount(1);
        result.CleanedTombstones[0].IssueId.Should().Be(issue.Id);
        result.CleanedTombstones[0].OriginalTitle.Should().Be("Doomed issue");
        result.CleanedTombstones[0].CleanedBy.Should().Be("test-user");
        result.CleanedTombstones[0].CleanedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        // Assert: issue no longer on disk
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().BeEmpty();

        // Assert: tombstone file exists on disk
        var tombstones = await _storageService.LoadTombstonesAsync();
        tombstones.Should().HaveCount(1);
        tombstones[0].IssueId.Should().Be(issue.Id);
        tombstones[0].OriginalTitle.Should().Be("Doomed issue");

        // Assert: tombstone file physically exists
        var tombstoneFiles = await _storageService.GetAllTombstoneFilesAsync();
        tombstoneFiles.Should().HaveCount(1);
    }

    [Test]
    public async Task Clean_PreservesNonDeletedIssues()
    {
        // Arrange
        var keepIssue = await _issueService.CreateAsync("Keep me", IssueType.Task);
        var deleteIssue = await _issueService.CreateAsync("Delete me", IssueType.Bug);
        await _issueService.DeleteAsync(deleteIssue.Id);

        // Act
        await _cleanService.CleanAsync();

        // Assert: only the non-deleted issue remains on disk
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].Id.Should().Be(keepIssue.Id);
        issuesOnDisk[0].Title.Should().Be("Keep me");
    }

    [Test]
    public async Task Clean_WithIncludeComplete_RemovesCompletedIssues()
    {
        // Arrange
        var completedIssue = await _issueService.CreateAsync("Done task", IssueType.Task);
        await _issueService.UpdateAsync(completedIssue.Id, status: IssueStatus.Complete);
        var openIssue = await _issueService.CreateAsync("Still open", IssueType.Task);

        // Act
        var result = await _cleanService.CleanAsync(includeComplete: true);

        // Assert
        result.CleanedTombstones.Should().HaveCount(1);
        result.CleanedTombstones[0].IssueId.Should().Be(completedIssue.Id);

        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].Id.Should().Be(openIssue.Id);
    }

    [Test]
    public async Task Clean_WithAllFlags_RemovesDeletedCompletedClosedArchived()
    {
        // Arrange
        var deletedIssue = await _issueService.CreateAsync("Deleted", IssueType.Task);
        await _issueService.DeleteAsync(deletedIssue.Id);

        var completedIssue = await _issueService.CreateAsync("Completed", IssueType.Task);
        await _issueService.UpdateAsync(completedIssue.Id, status: IssueStatus.Complete);

        var closedIssue = await _issueService.CreateAsync("Closed", IssueType.Task);
        await _issueService.UpdateAsync(closedIssue.Id, status: IssueStatus.Closed);

        var archivedIssue = await _issueService.CreateAsync("Archived", IssueType.Task);
        await _issueService.UpdateAsync(archivedIssue.Id, status: IssueStatus.Archived);

        var openIssue = await _issueService.CreateAsync("Still open", IssueType.Task);

        // Act
        var result = await _cleanService.CleanAsync(
            includeComplete: true,
            includeClosed: true,
            includeArchived: true);

        // Assert
        result.CleanedTombstones.Should().HaveCount(4);
        result.CleanedTombstones.Select(t => t.IssueId)
            .Should().BeEquivalentTo([deletedIssue.Id, completedIssue.Id, closedIssue.Id, archivedIssue.Id]);

        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].Id.Should().Be(openIssue.Id);

        var tombstones = await _storageService.LoadTombstonesAsync();
        tombstones.Should().HaveCount(4);
    }

    #endregion

    #region Create with same title generates salted ID

    [Test]
    public async Task Create_AfterClean_SameTitleGeneratesDifferentSaltedId()
    {
        // Arrange: create, delete, and clean an issue
        var original = await _issueService.CreateAsync("Recurring task", IssueType.Task);
        var originalId = original.Id;
        await _issueService.DeleteAsync(original.Id);
        await _cleanService.CleanAsync();

        // Verify the original is tombstoned
        var tombstones = await _storageService.LoadTombstonesAsync();
        tombstones.Should().ContainSingle(t => t.IssueId == originalId);

        // Act: create a new issue with the same title
        var recreated = await _issueService.CreateAsync("Recurring task", IssueType.Task);

        // Assert: the new issue has a different ID due to salted generation
        recreated.Id.Should().NotBe(originalId);
        recreated.Title.Should().Be("Recurring task");

        // Assert: both the tombstone and new issue exist
        var allTombstones = await _storageService.LoadTombstonesAsync();
        allTombstones.Should().ContainSingle(t => t.IssueId == originalId);

        var allIssues = await _storageService.LoadIssuesAsync();
        allIssues.Should().ContainSingle(i => i.Id == recreated.Id);
    }

    [Test]
    public async Task Create_AfterMultipleCleans_SameTitleStillGeneratesUniqueId()
    {
        // Clean the same title multiple times to accumulate tombstones
        var ids = new HashSet<string>();

        for (var i = 0; i < 3; i++)
        {
            var issue = await _issueService.CreateAsync("Repeated title", IssueType.Task);
            ids.Add(issue.Id);
            await _issueService.DeleteAsync(issue.Id);
            await _cleanService.CleanAsync();
        }

        // All generated IDs should be unique
        ids.Should().HaveCount(3, "each iteration should produce a unique ID via salting");

        // Creating yet another issue with the same title should still work
        var final = await _issueService.CreateAsync("Repeated title", IssueType.Task);
        ids.Should().NotContain(final.Id);

        // Tombstones should track all previous IDs
        var tombstones = await _storageService.LoadTombstonesAsync();
        tombstones.Should().HaveCount(3);
    }

    #endregion

    #region Merge consolidates tombstone files

    [Test]
    public async Task Merge_ConsolidatesMultipleTombstoneFiles()
    {
        // Arrange: create tombstone files by writing directly to simulate
        // multiple unmerged tombstone files from different team members
        var serializer = new JsonlSerializer();

        var tombstone1 = new Tombstone
        {
            IssueId = "tomb01",
            OriginalTitle = "First tombstoned issue",
            CleanedAt = DateTimeOffset.UtcNow.AddDays(-2),
            CleanedBy = "user-a"
        };

        var tombstone2 = new Tombstone
        {
            IssueId = "tomb02",
            OriginalTitle = "Second tombstoned issue",
            CleanedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CleanedBy = "user-b"
        };

        // Write two separate tombstone files
        var file1 = Path.Combine(_fleecePath, "tombstones_aaaaaa.jsonl");
        var file2 = Path.Combine(_fleecePath, "tombstones_bbbbbb.jsonl");
        await File.WriteAllTextAsync(file1, serializer.SerializeTombstone(tombstone1) + "\n");
        await File.WriteAllTextAsync(file2, serializer.SerializeTombstone(tombstone2) + "\n");

        // Also need at least one issue file for merge to process
        var issue = await _issueService.CreateAsync("Active issue", IssueType.Task);

        // Verify we have multiple tombstone files
        var tombstoneFiles = await _storageService.GetAllTombstoneFilesAsync();
        tombstoneFiles.Count.Should().BeGreaterThanOrEqualTo(2);

        // Act
        await _mergeService.FindAndResolveDuplicatesAsync();

        // Assert: tombstones are consolidated into a single file
        var tombstoneFilesAfter = await _storageService.GetAllTombstoneFilesAsync();
        tombstoneFilesAfter.Should().HaveCount(1);

        // Assert: all tombstones are preserved
        var allTombstones = await _storageService.LoadTombstonesAsync();
        allTombstones.Should().HaveCount(2);
        allTombstones.Select(t => t.IssueId).Should().BeEquivalentTo(["tomb01", "tomb02"]);
    }

    [Test]
    public async Task Merge_WithSingleTombstoneFile_DoesNotDuplicateOrLoseData()
    {
        // Arrange: create, delete, clean to produce a single tombstone file
        var issue = await _issueService.CreateAsync("Single tombstone test", IssueType.Task);
        await _issueService.DeleteAsync(issue.Id);
        await _cleanService.CleanAsync();

        var keepIssue = await _issueService.CreateAsync("Active issue", IssueType.Task);

        var tombstonesBefore = await _storageService.LoadTombstonesAsync();
        tombstonesBefore.Should().HaveCount(1);

        // Act
        await _mergeService.FindAndResolveDuplicatesAsync();

        // Assert: tombstone data preserved
        var tombstonesAfter = await _storageService.LoadTombstonesAsync();
        tombstonesAfter.Should().HaveCount(1);
        tombstonesAfter[0].IssueId.Should().Be(issue.Id);
    }

    #endregion

    #region Clean strips dangling references

    [Test]
    public async Task Clean_StripsLinkedIssueReferencesToDeletedIssue()
    {
        // Arrange: create two issues, link them, then delete one
        var issueA = await _issueService.CreateAsync("Issue A", IssueType.Task);
        var issueB = await _issueService.CreateAsync("Issue B", IssueType.Task);

        // Link B to A
        await _issueService.UpdateAsync(issueB.Id, linkedIssues: [issueA.Id]);

        // Delete A
        await _issueService.DeleteAsync(issueA.Id);

        // Act
        var result = await _cleanService.CleanAsync();

        // Assert: references were stripped
        result.StrippedReferences.Should().HaveCount(1);
        result.StrippedReferences[0].IssueId.Should().Be(issueA.Id);
        result.StrippedReferences[0].ReferencingIssueId.Should().Be(issueB.Id);
        result.StrippedReferences[0].ReferenceType.Should().Be("LinkedIssues");

        // Assert: on disk, issue B no longer references A
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].Id.Should().Be(issueB.Id);
        issuesOnDisk[0].LinkedIssues.Should().BeEmpty();
    }

    [Test]
    public async Task Clean_StripsParentIssueReferencesToDeletedIssue()
    {
        // Arrange: create parent and child, then delete parent
        var parent = await _issueService.CreateAsync("Parent issue", IssueType.Feature);
        var child = await _issueService.CreateAsync("Child issue", IssueType.Task);

        // Set parent reference
        await _issueService.UpdateAsync(child.Id, parentIssues: [
            new ParentIssueRef { ParentIssue = parent.Id, SortOrder = "aaa" }
        ]);

        // Delete parent
        await _issueService.DeleteAsync(parent.Id);

        // Act
        var result = await _cleanService.CleanAsync();

        // Assert: parent reference stripped
        result.StrippedReferences.Should().HaveCount(1);
        result.StrippedReferences[0].IssueId.Should().Be(parent.Id);
        result.StrippedReferences[0].ReferencingIssueId.Should().Be(child.Id);
        result.StrippedReferences[0].ReferenceType.Should().Be("ParentIssues");

        // Assert: on disk, child no longer references parent
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].Id.Should().Be(child.Id);
        issuesOnDisk[0].ParentIssues.Should().BeEmpty();
    }

    [Test]
    public async Task Clean_PreservesNonDanglingReferences()
    {
        // Arrange: A links to both B and C, delete only B
        var issueA = await _issueService.CreateAsync("Issue A", IssueType.Task);
        var issueB = await _issueService.CreateAsync("Issue B", IssueType.Task);
        var issueC = await _issueService.CreateAsync("Issue C", IssueType.Task);

        await _issueService.UpdateAsync(issueA.Id, linkedIssues: [issueB.Id, issueC.Id]);
        await _issueService.DeleteAsync(issueB.Id);

        // Act
        var result = await _cleanService.CleanAsync();

        // Assert: only the dangling reference to B was stripped
        result.StrippedReferences.Should().HaveCount(1);
        result.StrippedReferences[0].IssueId.Should().Be(issueB.Id);

        // Assert: on disk, issue A still references C
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        var issueAOnDisk = issuesOnDisk.First(i => i.Id == issueA.Id);
        issueAOnDisk.LinkedIssues.Should().ContainSingle(issueC.Id);
    }

    [Test]
    public async Task Clean_WithStripReferencesFalse_PreservesAllReferences()
    {
        // Arrange
        var issueA = await _issueService.CreateAsync("Issue A", IssueType.Task);
        var issueB = await _issueService.CreateAsync("Issue B", IssueType.Task);
        await _issueService.UpdateAsync(issueB.Id, linkedIssues: [issueA.Id]);
        await _issueService.DeleteAsync(issueA.Id);

        // Act: clean without stripping references
        var result = await _cleanService.CleanAsync(stripReferences: false);

        // Assert: no references were stripped
        result.StrippedReferences.Should().BeEmpty();

        // Assert: on disk, issue B still references deleted A (dangling)
        var issuesOnDisk = await _storageService.LoadIssuesAsync();
        issuesOnDisk.Should().ContainSingle();
        issuesOnDisk[0].LinkedIssues.Should().ContainSingle(issueA.Id);
    }

    #endregion

    #region Clean removes orphaned change records

    [Test]
    public async Task Clean_RemovesChangeRecordsForDeletedIssue()
    {
        // Arrange: create, update, and delete an issue (generates change records)
        var issue = await _issueService.CreateAsync("Issue with history", IssueType.Task);
        await _issueService.UpdateAsync(issue.Id, title: "Updated title");
        await _issueService.DeleteAsync(issue.Id);

        // Verify we have change records for this issue
        var changesBefore = await _storageService.LoadChangesAsync();
        var issueChanges = changesBefore.Count(c => c.IssueId == issue.Id);
        issueChanges.Should().BeGreaterThanOrEqualTo(3, "should have create + update + delete records");

        // Act
        var result = await _cleanService.CleanAsync();

        // Assert: result reports removed change records
        result.RemovedChangeRecords.Should().BeGreaterThanOrEqualTo(3);

        // Assert: on disk, no change records remain for the deleted issue
        var changesAfter = await _storageService.LoadChangesAsync();
        changesAfter.Where(c => c.IssueId == issue.Id).Should().BeEmpty();
    }

    [Test]
    public async Task Clean_PreservesChangeRecordsForSurvivingIssues()
    {
        // Arrange
        var keepIssue = await _issueService.CreateAsync("Keep me", IssueType.Task);
        var deleteIssue = await _issueService.CreateAsync("Delete me", IssueType.Bug);
        await _issueService.UpdateAsync(keepIssue.Id, title: "Keep me updated");
        await _issueService.DeleteAsync(deleteIssue.Id);

        var changesBefore = await _storageService.LoadChangesAsync();
        var keepChanges = changesBefore.Count(c => c.IssueId == keepIssue.Id);
        keepChanges.Should().BeGreaterThanOrEqualTo(2, "should have create + update records");

        // Act
        await _cleanService.CleanAsync();

        // Assert: change records for kept issue are preserved
        var changesAfter = await _storageService.LoadChangesAsync();
        changesAfter.Where(c => c.IssueId == keepIssue.Id).Should().HaveCountGreaterThanOrEqualTo(2);

        // Assert: change records for deleted issue are gone
        changesAfter.Where(c => c.IssueId == deleteIssue.Id).Should().BeEmpty();
    }

    #endregion

    #region Full end-to-end workflow

    [Test]
    public async Task FullWorkflow_Delete_DryRun_Clean_Recreate_Merge()
    {
        // Step 1: Create issues
        var issue1 = await _issueService.CreateAsync("Feature: login page", IssueType.Feature);
        var issue2 = await _issueService.CreateAsync("Bug: crash on save", IssueType.Bug);
        var issue3 = await _issueService.CreateAsync("Task: update docs", IssueType.Task);

        // Link issue3 to issue1
        await _issueService.UpdateAsync(issue3.Id, linkedIssues: [issue1.Id]);

        // Step 2: Soft-delete issue1
        await _issueService.DeleteAsync(issue1.Id);

        // Step 3: Dry-run shows issue1 and the dangling reference
        var dryRunResult = await _cleanService.CleanAsync(dryRun: true);
        dryRunResult.CleanedTombstones.Should().HaveCount(1);
        dryRunResult.CleanedTombstones[0].IssueId.Should().Be(issue1.Id);
        dryRunResult.StrippedReferences.Should().HaveCount(1);

        // Disk is unchanged
        var issuesAfterDryRun = await _storageService.LoadIssuesAsync();
        issuesAfterDryRun.Should().HaveCount(3);

        // Step 4: Actually clean
        var cleanResult = await _cleanService.CleanAsync();
        cleanResult.CleanedTombstones.Should().HaveCount(1);
        cleanResult.StrippedReferences.Should().HaveCount(1);
        cleanResult.RemovedChangeRecords.Should().BeGreaterThan(0);

        // Issue1 is removed, issue3 no longer references it
        var issuesAfterClean = await _storageService.LoadIssuesAsync();
        issuesAfterClean.Should().HaveCount(2);
        issuesAfterClean.Select(i => i.Id).Should().BeEquivalentTo([issue2.Id, issue3.Id]);

        var issue3AfterClean = issuesAfterClean.First(i => i.Id == issue3.Id);
        issue3AfterClean.LinkedIssues.Should().BeEmpty();

        // Tombstone exists
        var tombstones = await _storageService.LoadTombstonesAsync();
        tombstones.Should().ContainSingle(t => t.IssueId == issue1.Id);

        // Step 5: Recreate with same title gets salted ID
        var recreated = await _issueService.CreateAsync("Feature: login page", IssueType.Feature);
        recreated.Id.Should().NotBe(issue1.Id);

        // Step 6: Merge consolidates everything
        await _mergeService.FindAndResolveDuplicatesAsync();

        // Verify final state
        var finalIssues = await _storageService.LoadIssuesAsync();
        finalIssues.Should().HaveCount(3);
        finalIssues.Select(i => i.Title).Should().Contain("Feature: login page");

        var finalTombstones = await _storageService.LoadTombstonesAsync();
        finalTombstones.Should().ContainSingle(t => t.IssueId == issue1.Id);

        var finalTombstoneFiles = await _storageService.GetAllTombstoneFilesAsync();
        finalTombstoneFiles.Should().HaveCount(1);
    }

    [Test]
    public async Task NothingToClean_ReturnsEmptyResult_DiskUnchanged()
    {
        // Arrange: only active issues
        var issue1 = await _issueService.CreateAsync("Active one", IssueType.Task);
        var issue2 = await _issueService.CreateAsync("Active two", IssueType.Bug);

        var issuesBefore = await _storageService.LoadIssuesAsync();

        // Act
        var result = await _cleanService.CleanAsync();

        // Assert
        result.CleanedTombstones.Should().BeEmpty();
        result.StrippedReferences.Should().BeEmpty();
        result.RemovedChangeRecords.Should().Be(0);

        var issuesAfter = await _storageService.LoadIssuesAsync();
        issuesAfter.Should().HaveCount(issuesBefore.Count);
    }

    #endregion
}
