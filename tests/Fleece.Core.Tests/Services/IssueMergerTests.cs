using Fleece.Core.Models;
using Fleece.Core.Services;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class IssueMergerTests
{
    private IssueMerger _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new IssueMerger();
    }

    [Test]
    public void Merge_WithDuplicateParentIssuesInListA_DeduplicatesAndSucceeds()
    {
        // Arrange - Issue A has duplicate parent references
        var issueA = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Issue")
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "a" },
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "b" } // Duplicate!
            )
            .Build();

        var issueB = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Issue")
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parent2", SortOrder = "c" }
            )
            .Build();

        // Act
        var result = _sut.Merge(issueA, issueB);

        // Assert
        result.MergedIssue.ParentIssues.Should().HaveCount(2);
        result.MergedIssue.ParentIssues.Select(p => p.ParentIssue)
            .Should().BeEquivalentTo(["parent1", "parent2"]);
    }

    [Test]
    public void Merge_WithDuplicateParentIssuesInListB_DeduplicatesAndSucceeds()
    {
        // Arrange - Issue B has duplicate parent references
        var issueA = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Issue")
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "a" }
            )
            .Build();

        var issueB = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Issue")
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parent2", SortOrder = "b" },
                new ParentIssueRef { ParentIssue = "parent2", SortOrder = "c" } // Duplicate!
            )
            .Build();

        // Act
        var result = _sut.Merge(issueA, issueB);

        // Assert
        result.MergedIssue.ParentIssues.Should().HaveCount(2);
        result.MergedIssue.ParentIssues.Select(p => p.ParentIssue)
            .Should().BeEquivalentTo(["parent1", "parent2"]);
    }

    [Test]
    public void Merge_WithDuplicateParentIssuesInBothLists_DeduplicatesAndSucceeds()
    {
        // Arrange - Both issues have duplicate parent references
        var issueA = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Issue")
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "a" },
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "b" } // Duplicate!
            )
            .Build();

        var issueB = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Issue")
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parent2", SortOrder = "c" },
                new ParentIssueRef { ParentIssue = "parent2", SortOrder = "d" } // Duplicate!
            )
            .Build();

        // Act
        var result = _sut.Merge(issueA, issueB);

        // Assert
        result.MergedIssue.ParentIssues.Should().HaveCount(2);
        result.MergedIssue.ParentIssues.Select(p => p.ParentIssue)
            .Should().BeEquivalentTo(["parent1", "parent2"]);
    }

    [Test]
    public void Merge_WithDuplicateParentIssues_KeepsFirstOccurrenceSortOrder()
    {
        // Arrange - Issue A has duplicate parent references with different sort orders
        var issueA = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Issue")
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "first" },
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "second" } // Duplicate - should be ignored
            )
            .Build();

        var issueB = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Issue")
            .WithParentIssues()
            .Build();

        // Act
        var result = _sut.Merge(issueA, issueB);

        // Assert
        result.MergedIssue.ParentIssues.Should().HaveCount(1);
        result.MergedIssue.ParentIssues[0].ParentIssue.Should().Be("parent1");
        result.MergedIssue.ParentIssues[0].SortOrder.Should().Be("first");
    }

    [Test]
    public void Merge_ExecutionMode_BothSetSameValue_KeepsNewerTimestamp()
    {
        // Arrange - both set to Parallel, A has newer timestamp
        var olderTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10);
        var newerTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var issueA = new IssueBuilder()
            .WithId("test1")
            .WithTitle("Test Issue")
            .WithExecutionMode(ExecutionMode.Parallel, newerTimestamp, "user-a")
            .Build();

        var issueB = new IssueBuilder()
            .WithId("test1")
            .WithTitle("Test Issue")
            .WithExecutionMode(ExecutionMode.Parallel, olderTimestamp, "user-b")
            .Build();

        // Act
        var result = _sut.Merge(issueA, issueB);

        // Assert
        result.MergedIssue.ExecutionMode.Should().Be(ExecutionMode.Parallel);
        result.MergedIssue.ExecutionModeLastUpdate.Should().Be(newerTimestamp);
        result.MergedIssue.ExecutionModeModifiedBy.Should().Be("user-a");
        result.PropertyChanges.Should().NotContain(c => c.PropertyName == "ExecutionMode");
    }

    [Test]
    public void Merge_WithCaseInsensitiveDuplicateParentIssues_DeduplicatesCorrectly()
    {
        // Arrange - Issue A has case-insensitive duplicate parent references
        var issueA = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Issue")
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "PARENT1", SortOrder = "a" },
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "b" } // Case-insensitive duplicate!
            )
            .Build();

        var issueB = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Issue")
            .WithParentIssues()
            .Build();

        // Act
        var result = _sut.Merge(issueA, issueB);

        // Assert
        result.MergedIssue.ParentIssues.Should().HaveCount(1);
        // Should keep the first occurrence (PARENT1)
        result.MergedIssue.ParentIssues[0].ParentIssue.Should().Be("PARENT1");
    }

    [Test]
    public void Merge_ExecutionMode_BothDefaultWithNoTimestamp_KeepsSeriesWithNullTimestamp()
    {
        // Arrange - Neither side has explicitly set ExecutionMode
        var baseIssue = new IssueBuilder().WithId("test1").WithTitle("Test").Build();
        var issueA = baseIssue with { ExecutionMode = ExecutionMode.Series, ExecutionModeLastUpdate = null, ExecutionModeModifiedBy = null };
        var issueB = baseIssue with { ExecutionMode = ExecutionMode.Series, ExecutionModeLastUpdate = null, ExecutionModeModifiedBy = null };

        // Act
        var result = _sut.Merge(issueA, issueB);

        // Assert
        result.MergedIssue.ExecutionMode.Should().Be(ExecutionMode.Series);
        result.MergedIssue.ExecutionModeLastUpdate.Should().BeNull();
        result.MergedIssue.ExecutionModeModifiedBy.Should().BeNull();
    }

    [Test]
    public void Merge_ExecutionMode_OneSideSetOtherDefault_SetSideWins()
    {
        // Arrange - Issue A has Parallel with timestamp, Issue B has default Series with no timestamp
        var timestamp = DateTimeOffset.UtcNow;
        var baseIssue = new IssueBuilder().WithId("test1").WithTitle("Test").Build();
        var issueA = baseIssue with { ExecutionMode = ExecutionMode.Parallel, ExecutionModeLastUpdate = timestamp, ExecutionModeModifiedBy = "user-a" };
        var issueB = baseIssue with { ExecutionMode = ExecutionMode.Series, ExecutionModeLastUpdate = null, ExecutionModeModifiedBy = null };

        // Act
        var result = _sut.Merge(issueA, issueB);

        // Assert
        result.MergedIssue.ExecutionMode.Should().Be(ExecutionMode.Parallel);
        result.MergedIssue.ExecutionModeLastUpdate.Should().Be(timestamp);
        result.MergedIssue.ExecutionModeModifiedBy.Should().Be("user-a");
    }

    [Test]
    public void Merge_ExecutionMode_BothSetDifferentValues_NewerTimestampWins()
    {
        // Arrange - Both sides set, A is newer
        var olderTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10);
        var newerTimestamp = DateTimeOffset.UtcNow;
        var baseIssue = new IssueBuilder().WithId("test1").WithTitle("Test").Build();
        var issueA = baseIssue with { ExecutionMode = ExecutionMode.Parallel, ExecutionModeLastUpdate = newerTimestamp, ExecutionModeModifiedBy = "user-a" };
        var issueB = baseIssue with { ExecutionMode = ExecutionMode.Series, ExecutionModeLastUpdate = olderTimestamp, ExecutionModeModifiedBy = "user-b" };

        // Act
        var result = _sut.Merge(issueA, issueB);

        // Assert
        result.MergedIssue.ExecutionMode.Should().Be(ExecutionMode.Parallel);
        result.MergedIssue.ExecutionModeLastUpdate.Should().Be(newerTimestamp);
    }

    [Test]
    public void Merge_WorkingBranchId_OneSideSetOtherNull_SetSideWins()
    {
        // Arrange - Issue A has WorkingBranchId, Issue B does not
        var timestamp = DateTimeOffset.UtcNow;
        var baseIssue = new IssueBuilder().WithId("test1").WithTitle("Test").Build();
        var issueA = baseIssue with { WorkingBranchId = "feature-branch", WorkingBranchIdLastUpdate = timestamp, WorkingBranchIdModifiedBy = "user-a" };
        var issueB = baseIssue with { WorkingBranchId = null, WorkingBranchIdLastUpdate = null, WorkingBranchIdModifiedBy = null };

        // Act
        var result = _sut.Merge(issueA, issueB);

        // Assert
        result.MergedIssue.WorkingBranchId.Should().Be("feature-branch");
        result.MergedIssue.WorkingBranchIdLastUpdate.Should().Be(timestamp);
        result.MergedIssue.WorkingBranchIdModifiedBy.Should().Be("user-a");
    }

    [Test]
    public void Merge_WorkingBranchId_BothSetDifferentValues_NewerTimestampWins()
    {
        // Arrange - Both sides set, B is newer
        var olderTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10);
        var newerTimestamp = DateTimeOffset.UtcNow;
        var baseIssue = new IssueBuilder().WithId("test1").WithTitle("Test").Build();
        var issueA = baseIssue with { WorkingBranchId = "old-branch", WorkingBranchIdLastUpdate = olderTimestamp, WorkingBranchIdModifiedBy = "user-a" };
        var issueB = baseIssue with { WorkingBranchId = "new-branch", WorkingBranchIdLastUpdate = newerTimestamp, WorkingBranchIdModifiedBy = "user-b" };

        // Act
        var result = _sut.Merge(issueA, issueB);

        // Assert
        result.MergedIssue.WorkingBranchId.Should().Be("new-branch");
        result.MergedIssue.WorkingBranchIdLastUpdate.Should().Be(newerTimestamp);
    }
}
