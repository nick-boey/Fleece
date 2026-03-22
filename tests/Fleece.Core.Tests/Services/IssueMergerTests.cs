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
}
