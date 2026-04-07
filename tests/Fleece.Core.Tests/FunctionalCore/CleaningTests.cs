using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.FunctionalCore;

[TestFixture]
public class CleaningTests
{
    [Test]
    public void CleansOnlyDeletedByDefault()
    {
        var deletedIssue = new IssueBuilder().WithId("del001").WithStatus(IssueStatus.Deleted).Build();
        var openIssue = new IssueBuilder().WithId("opn001").WithStatus(IssueStatus.Open).Build();
        var completeIssue = new IssueBuilder().WithId("cmp001").WithStatus(IssueStatus.Complete).Build();

        var plan = Cleaning.Plan(
            [deletedIssue, openIssue, completeIssue],
            existingTombstones: [],
            includeComplete: false, includeClosed: false, includeArchived: false,
            stripReferences: true, DateTimeOffset.UtcNow, "Test User");

        plan.TombstonesToCreate.Should().HaveCount(1);
        plan.TombstonesToCreate[0].IssueId.Should().Be("del001");
    }

    [Test]
    public void DoesNotCleanNonDeletedUnlessFlagsSet()
    {
        var completeIssue = new IssueBuilder().WithId("cmp001").WithStatus(IssueStatus.Complete).Build();
        var closedIssue = new IssueBuilder().WithId("cls001").WithStatus(IssueStatus.Closed).Build();
        var archivedIssue = new IssueBuilder().WithId("arc001").WithStatus(IssueStatus.Archived).Build();

        var plan = Cleaning.Plan(
            [completeIssue, closedIssue, archivedIssue],
            existingTombstones: [],
            includeComplete: false, includeClosed: false, includeArchived: false,
            stripReferences: true, DateTimeOffset.UtcNow, "Test User");

        plan.TombstonesToCreate.Should().BeEmpty();
    }

    [Test]
    public void IncludesCompleteWhenFlagSet()
    {
        var completeIssue = new IssueBuilder().WithId("cmp001").WithStatus(IssueStatus.Complete).Build();
        var openIssue = new IssueBuilder().WithId("opn001").WithStatus(IssueStatus.Open).Build();

        var plan = Cleaning.Plan(
            [completeIssue, openIssue],
            existingTombstones: [],
            includeComplete: true, includeClosed: false, includeArchived: false,
            stripReferences: true, DateTimeOffset.UtcNow, "Test User");

        plan.TombstonesToCreate.Should().HaveCount(1);
        plan.TombstonesToCreate[0].IssueId.Should().Be("cmp001");
    }

    [Test]
    public void IncludesClosedWhenFlagSet()
    {
        var closedIssue = new IssueBuilder().WithId("cls001").WithStatus(IssueStatus.Closed).Build();

        var plan = Cleaning.Plan(
            [closedIssue],
            existingTombstones: [],
            includeComplete: false, includeClosed: true, includeArchived: false,
            stripReferences: true, DateTimeOffset.UtcNow, "Test User");

        plan.TombstonesToCreate.Should().HaveCount(1);
        plan.TombstonesToCreate[0].IssueId.Should().Be("cls001");
    }

    [Test]
    public void IncludesArchivedWhenFlagSet()
    {
        var archivedIssue = new IssueBuilder().WithId("arc001").WithStatus(IssueStatus.Archived).Build();

        var plan = Cleaning.Plan(
            [archivedIssue],
            existingTombstones: [],
            includeComplete: false, includeClosed: false, includeArchived: true,
            stripReferences: true, DateTimeOffset.UtcNow, "Test User");

        plan.TombstonesToCreate.Should().HaveCount(1);
        plan.TombstonesToCreate[0].IssueId.Should().Be("arc001");
    }

    [Test]
    public void CreatesTombstonesWithCorrectFields()
    {
        var deletedIssue = new IssueBuilder()
            .WithId("del001")
            .WithTitle("My deleted issue")
            .WithStatus(IssueStatus.Deleted)
            .Build();

        var now = DateTimeOffset.UtcNow;
        var plan = Cleaning.Plan(
            [deletedIssue],
            existingTombstones: [],
            includeComplete: false, includeClosed: false, includeArchived: false,
            stripReferences: true, now, "Test User");

        plan.TombstonesToCreate.Should().HaveCount(1);
        var tombstone = plan.TombstonesToCreate[0];
        tombstone.IssueId.Should().Be("del001");
        tombstone.OriginalTitle.Should().Be("My deleted issue");
        tombstone.CleanedBy.Should().Be("Test User");
        tombstone.CleanedAt.Should().Be(now);
    }

    [Test]
    public void StripsLinkedIssuesReferences()
    {
        var deletedIssue = new IssueBuilder().WithId("del001").WithStatus(IssueStatus.Deleted).Build();
        var referencingIssue = new IssueBuilder()
            .WithId("opn001")
            .WithStatus(IssueStatus.Open)
            .WithLinkedIssues("del001", "opn002")
            .Build();

        var plan = Cleaning.Plan(
            [deletedIssue, referencingIssue],
            existingTombstones: [],
            includeComplete: false, includeClosed: false, includeArchived: false,
            stripReferences: true, DateTimeOffset.UtcNow, "Test User");

        plan.StrippedReferences.Should().HaveCount(1);
        plan.StrippedReferences[0].IssueId.Should().Be("del001");
        plan.StrippedReferences[0].ReferencingIssueId.Should().Be("opn001");
        plan.StrippedReferences[0].ReferenceType.Should().Be("LinkedIssues");

        plan.UpdatedIssues.Should().HaveCount(1);
        plan.UpdatedIssues[0].LinkedIssues.Should().HaveCount(1);
        plan.UpdatedIssues[0].LinkedIssues[0].Should().Be("opn002");
    }

    [Test]
    public void StripsParentIssuesReferences()
    {
        var deletedIssue = new IssueBuilder().WithId("del001").WithStatus(IssueStatus.Deleted).Build();
        var childIssue = new IssueBuilder()
            .WithId("opn001")
            .WithStatus(IssueStatus.Open)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "del001", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "opn002", SortOrder = "bbb" })
            .Build();

        var plan = Cleaning.Plan(
            [deletedIssue, childIssue],
            existingTombstones: [],
            includeComplete: false, includeClosed: false, includeArchived: false,
            stripReferences: true, DateTimeOffset.UtcNow, "Test User");

        plan.StrippedReferences.Should().HaveCount(1);
        plan.StrippedReferences[0].IssueId.Should().Be("del001");
        plan.StrippedReferences[0].ReferencingIssueId.Should().Be("opn001");
        plan.StrippedReferences[0].ReferenceType.Should().Be("ParentIssues");

        plan.UpdatedIssues.Should().HaveCount(1);
        plan.UpdatedIssues[0].ParentIssues.Should().HaveCount(1);
        plan.UpdatedIssues[0].ParentIssues[0].ParentIssue.Should().Be("opn002");
    }

    [Test]
    public void RespectsStripReferencesFalse()
    {
        var deletedIssue = new IssueBuilder().WithId("del001").WithStatus(IssueStatus.Deleted).Build();
        var referencingIssue = new IssueBuilder()
            .WithId("opn001")
            .WithStatus(IssueStatus.Open)
            .WithLinkedIssues("del001")
            .Build();

        var plan = Cleaning.Plan(
            [deletedIssue, referencingIssue],
            existingTombstones: [],
            includeComplete: false, includeClosed: false, includeArchived: false,
            stripReferences: false, DateTimeOffset.UtcNow, "Test User");

        plan.StrippedReferences.Should().BeEmpty();
        plan.UpdatedIssues.Should().HaveCount(1);
        plan.UpdatedIssues[0].LinkedIssues.Should().HaveCount(1);
        plan.UpdatedIssues[0].LinkedIssues[0].Should().Be("del001");
    }

    [Test]
    public void ReturnsEmptyResultWhenNothingToClean()
    {
        var openIssue = new IssueBuilder().WithId("opn001").WithStatus(IssueStatus.Open).Build();

        var plan = Cleaning.Plan(
            [openIssue],
            existingTombstones: [],
            includeComplete: false, includeClosed: false, includeArchived: false,
            stripReferences: true, DateTimeOffset.UtcNow, "Test User");

        plan.TombstonesToCreate.Should().BeEmpty();
        plan.StrippedReferences.Should().BeEmpty();
    }

    [Test]
    public void CleansMultipleStatusesWhenAllFlagsSet()
    {
        var deletedIssue = new IssueBuilder().WithId("del001").WithStatus(IssueStatus.Deleted).Build();
        var completeIssue = new IssueBuilder().WithId("cmp001").WithStatus(IssueStatus.Complete).Build();
        var closedIssue = new IssueBuilder().WithId("cls001").WithStatus(IssueStatus.Closed).Build();
        var archivedIssue = new IssueBuilder().WithId("arc001").WithStatus(IssueStatus.Archived).Build();
        var openIssue = new IssueBuilder().WithId("opn001").WithStatus(IssueStatus.Open).Build();

        var plan = Cleaning.Plan(
            [deletedIssue, completeIssue, closedIssue, archivedIssue, openIssue],
            existingTombstones: [],
            includeComplete: true, includeClosed: true, includeArchived: true,
            stripReferences: true, DateTimeOffset.UtcNow, "Test User");

        plan.TombstonesToCreate.Should().HaveCount(4);
        plan.TombstonesToCreate.Select(t => t.IssueId)
            .Should().BeEquivalentTo(["del001", "cmp001", "cls001", "arc001"]);
    }
}
