using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.FunctionalCore;

[TestFixture]
public class DependenciesTests
{
    #region AddDependency

    [Test]
    public void AddDependency_DefaultPosition_AppendsAtEnd()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var existingChild = new IssueBuilder().WithId("existing1")
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var child = new IssueBuilder().WithId("child1").Build();
        var allIssues = new List<Issue> { parent, existingChild, child };

        // Act
        var result = Dependencies.AddDependency(child, "parent1", allIssues);

        // Assert - sort order should be after "aaa"
        result.Id.Should().Be("child1");
        result.ParentIssues.Should().HaveCount(1);
        result.ParentIssues[0].ParentIssue.Should().Be("parent1");
        string.Compare(result.ParentIssues[0].SortOrder, "aaa", StringComparison.Ordinal)
            .Should().BePositive();
    }

    [Test]
    public void AddDependency_FirstPosition_PrependsAtBeginning()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var existingChild = new IssueBuilder().WithId("existing1")
            .WithParentIssueIdAndOrder("parent1", "nnn").Build();
        var child = new IssueBuilder().WithId("child1").Build();
        var allIssues = new List<Issue> { parent, existingChild, child };

        // Act
        var result = Dependencies.AddDependency(child, "parent1", allIssues,
            new DependencyPosition { Kind = DependencyPositionKind.First });

        // Assert - sort order should be before "nnn"
        result.Id.Should().Be("child1");
        result.ParentIssues.Should().HaveCount(1);
        result.ParentIssues[0].ParentIssue.Should().Be("parent1");
        string.Compare(result.ParentIssues[0].SortOrder, "nnn", StringComparison.Ordinal)
            .Should().BeNegative();
    }

    [Test]
    public void AddDependency_AfterPosition_PlacesAfterSibling()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var sibling1 = new IssueBuilder().WithId("sibling1")
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var sibling2 = new IssueBuilder().WithId("sibling2")
            .WithParentIssueIdAndOrder("parent1", "nnn").Build();
        var child = new IssueBuilder().WithId("child1").Build();
        var allIssues = new List<Issue> { parent, sibling1, sibling2, child };

        // Act
        var result = Dependencies.AddDependency(child, "parent1", allIssues,
            new DependencyPosition { Kind = DependencyPositionKind.After, SiblingId = "sibling1" });

        // Assert - sort order should be between "aaa" and "nnn"
        result.ParentIssues.Should().HaveCount(1);
        result.ParentIssues[0].ParentIssue.Should().Be("parent1");
        string.Compare(result.ParentIssues[0].SortOrder, "aaa", StringComparison.Ordinal)
            .Should().BePositive();
        string.Compare(result.ParentIssues[0].SortOrder, "nnn", StringComparison.Ordinal)
            .Should().BeNegative();
    }

    [Test]
    public void AddDependency_BeforePosition_PlacesBeforeSibling()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var sibling1 = new IssueBuilder().WithId("sibling1")
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var sibling2 = new IssueBuilder().WithId("sibling2")
            .WithParentIssueIdAndOrder("parent1", "nnn").Build();
        var child = new IssueBuilder().WithId("child1").Build();
        var allIssues = new List<Issue> { parent, sibling1, sibling2, child };

        // Act
        var result = Dependencies.AddDependency(child, "parent1", allIssues,
            new DependencyPosition { Kind = DependencyPositionKind.Before, SiblingId = "sibling2" });

        // Assert - sort order should be between "aaa" and "nnn"
        result.ParentIssues.Should().HaveCount(1);
        result.ParentIssues[0].ParentIssue.Should().Be("parent1");
        string.Compare(result.ParentIssues[0].SortOrder, "aaa", StringComparison.Ordinal)
            .Should().BePositive();
        string.Compare(result.ParentIssues[0].SortOrder, "nnn", StringComparison.Ordinal)
            .Should().BeNegative();
    }

    [Test]
    public void AddDependency_AfterPosition_SiblingNotChildOfParent_ThrowsInvalidOperationException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1").Build();
        var notASibling = new IssueBuilder().WithId("notsibling").Build();
        var allIssues = new List<Issue> { parent, child, notASibling };

        // Act
        var act = () => Dependencies.AddDependency(child, "parent1", allIssues,
            new DependencyPosition { Kind = DependencyPositionKind.After, SiblingId = "notsibling" });

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a child*");
    }

    [Test]
    public void AddDependency_DuplicateRelationship_ThrowsInvalidOperationException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1").WithParentIssueIds("parent1").Build();
        var allIssues = new List<Issue> { parent, child };

        // Act
        var act = () => Dependencies.AddDependency(child, "parent1", allIssues);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already a child*");
    }

    [Test]
    public void AddDependency_NoExistingSiblings_AssignsSortOrder()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1").Build();
        var allIssues = new List<Issue> { parent, child };

        // Act
        var result = Dependencies.AddDependency(child, "parent1", allIssues);

        // Assert
        result.ParentIssues.Should().HaveCount(1);
        result.ParentIssues[0].ParentIssue.Should().Be("parent1");
        result.ParentIssues[0].SortOrder.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region AddDependency_ReplaceExisting

    [Test]
    public void AddDependency_ReplaceExisting_True_ReplacesAllParents()
    {
        // Arrange
        var parent1 = new IssueBuilder().WithId("parent1").Build();
        var parent2 = new IssueBuilder().WithId("parent2").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();
        var allIssues = new List<Issue> { parent1, parent2, child };

        // Act
        var result = Dependencies.AddDependency(child, "parent2", allIssues, replaceExisting: true);

        // Assert - should only have parent2, not parent1
        result.Id.Should().Be("child1");
        result.ParentIssues.Should().HaveCount(1);
        result.ParentIssues[0].ParentIssue.Should().Be("parent2");
    }

    [Test]
    public void AddDependency_ReplaceExisting_False_PreservesExistingParents()
    {
        // Arrange
        var parent1 = new IssueBuilder().WithId("parent1").Build();
        var parent2 = new IssueBuilder().WithId("parent2").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();
        var allIssues = new List<Issue> { parent1, parent2, child };

        // Act
        var result = Dependencies.AddDependency(child, "parent2", allIssues, replaceExisting: false);

        // Assert - should have both parent1 and parent2
        result.Id.Should().Be("child1");
        result.ParentIssues.Should().HaveCount(2);
        result.ParentIssues[0].ParentIssue.Should().Be("parent1");
        result.ParentIssues[1].ParentIssue.Should().Be("parent2");
    }

    [Test]
    public void AddDependency_ReplaceExisting_AllowsSameParent()
    {
        // Arrange - child already has parent1, re-adding with replace should succeed
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();
        var allIssues = new List<Issue> { parent, child };

        // Act - should not throw even though relationship exists
        var result = Dependencies.AddDependency(child, "parent1", allIssues, replaceExisting: true);

        // Assert
        result.Id.Should().Be("child1");
        result.ParentIssues.Should().HaveCount(1);
        result.ParentIssues[0].ParentIssue.Should().Be("parent1");
    }

    [Test]
    public void AddDependency_NotReplacing_DuplicateRelationship_ThrowsInvalidOperationException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1").WithParentIssueIds("parent1").Build();
        var allIssues = new List<Issue> { parent, child };

        // Act
        var act = () => Dependencies.AddDependency(child, "parent1", allIssues, replaceExisting: false);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already a child*");
    }

    #endregion

    #region AddDependency_MakePrimary

    [Test]
    public void AddDependency_MakePrimary_True_InsertsAtFront()
    {
        // Arrange
        var parent1 = new IssueBuilder().WithId("parent1").Build();
        var parent2 = new IssueBuilder().WithId("parent2").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();
        var allIssues = new List<Issue> { parent1, parent2, child };

        // Act
        var result = Dependencies.AddDependency(child, "parent2", allIssues, makePrimary: true);

        // Assert - parent2 should be first (primary), then parent1
        result.Id.Should().Be("child1");
        result.ParentIssues.Should().HaveCount(2);
        result.ParentIssues[0].ParentIssue.Should().Be("parent2");
        result.ParentIssues[1].ParentIssue.Should().Be("parent1");
    }

    [Test]
    public void AddDependency_MakePrimary_False_AppendsAtEnd()
    {
        // Arrange
        var parent1 = new IssueBuilder().WithId("parent1").Build();
        var parent2 = new IssueBuilder().WithId("parent2").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();
        var allIssues = new List<Issue> { parent1, parent2, child };

        // Act
        var result = Dependencies.AddDependency(child, "parent2", allIssues, makePrimary: false);

        // Assert - parent1 should be first, then parent2
        result.Id.Should().Be("child1");
        result.ParentIssues.Should().HaveCount(2);
        result.ParentIssues[0].ParentIssue.Should().Be("parent1");
        result.ParentIssues[1].ParentIssue.Should().Be("parent2");
    }

    [Test]
    public void AddDependency_ReplaceAndMakePrimary_Together()
    {
        // Arrange - child has parent1, replace with parent2 as primary
        var parent1 = new IssueBuilder().WithId("parent1").Build();
        var parent2 = new IssueBuilder().WithId("parent2").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();
        var allIssues = new List<Issue> { parent1, parent2, child };

        // Act
        var result = Dependencies.AddDependency(child, "parent2", allIssues,
            replaceExisting: true, makePrimary: true);

        // Assert - should only have parent2 (parent1 replaced)
        result.Id.Should().Be("child1");
        result.ParentIssues.Should().HaveCount(1);
        result.ParentIssues[0].ParentIssue.Should().Be("parent2");
    }

    #endregion

    #region RemoveDependency

    [Test]
    public void RemoveDependency_SuccessfulRemove_ReturnsUpdatedIssue()
    {
        // Arrange
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();

        // Act
        var result = Dependencies.RemoveDependency(child, "parent1");

        // Assert
        result.Id.Should().Be("child1");
        result.ParentIssues.Should().BeEmpty();
    }

    [Test]
    public void RemoveDependency_RelationshipDoesNotExist_ThrowsInvalidOperationException()
    {
        // Arrange
        var child = new IssueBuilder().WithId("child1").Build(); // no parent issues

        // Act
        var act = () => Dependencies.RemoveDependency(child, "parent1");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a child*");
    }

    [Test]
    public void RemoveDependency_PreservesOtherParents()
    {
        // Arrange
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1", "parent2").Build();

        // Act
        var result = Dependencies.RemoveDependency(child, "parent1");

        // Assert - should still have parent2
        result.Id.Should().Be("child1");
        result.ParentIssues.Should().HaveCount(1);
        result.ParentIssues[0].ParentIssue.Should().Be("parent2");
    }

    #endregion

    #region MoveUp

    [Test]
    public void MoveUp_SuccessfulMoveUp_ReturnsMoved()
    {
        // Arrange - 3 siblings, move the middle one up
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child1 = new IssueBuilder().WithId("child1")
            .WithParentIssueIdAndOrder("parent1", "ddd").Build();
        var child2 = new IssueBuilder().WithId("child2")
            .WithParentIssueIdAndOrder("parent1", "nnn").Build();
        var child3 = new IssueBuilder().WithId("child3")
            .WithParentIssueIdAndOrder("parent1", "zzz").Build();
        var allIssues = new List<Issue> { parent, child1, child2, child3 };

        // Act
        var (moved, siblings) = Dependencies.MoveUp(child2, "parent1", allIssues);

        // Assert - moved issue should have sort order before "ddd"
        moved.Id.Should().Be("child2");
        var sortOrder = moved.ParentIssues
            .First(p => p.ParentIssue == "parent1").SortOrder;
        string.Compare(sortOrder, "ddd", StringComparison.Ordinal)
            .Should().BeNegative();
    }

    [Test]
    public void MoveUp_AlreadyAtTop_ThrowsInvalidOperationException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child1 = new IssueBuilder().WithId("child1")
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2")
            .WithParentIssueIdAndOrder("parent1", "bbb").Build();
        var allIssues = new List<Issue> { parent, child1, child2 };

        // Act
        var act = () => Dependencies.MoveUp(child1, "parent1", allIssues);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already at the top*");
    }

    [Test]
    public void MoveUp_NotAChildOfParent_ThrowsInvalidOperationException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child1 = new IssueBuilder().WithId("child1").Build(); // no parent issues
        var allIssues = new List<Issue> { parent, child1 };

        // Act
        var act = () => Dependencies.MoveUp(child1, "parent1", allIssues);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a child*");
    }

    [Test]
    public void MoveUp_TwoSiblings_MovesSecondBeforeFirst()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child1 = new IssueBuilder().WithId("child1")
            .WithParentIssueIdAndOrder("parent1", "nnn").Build();
        var child2 = new IssueBuilder().WithId("child2")
            .WithParentIssueIdAndOrder("parent1", "zzz").Build();
        var allIssues = new List<Issue> { parent, child1, child2 };

        // Act
        var (moved, _) = Dependencies.MoveUp(child2, "parent1", allIssues);

        // Assert
        moved.Id.Should().Be("child2");
        var sortOrder = moved.ParentIssues
            .First(p => p.ParentIssue == "parent1").SortOrder;
        string.Compare(sortOrder, "nnn", StringComparison.Ordinal)
            .Should().BeNegative();
    }

    [Test]
    public void MoveUp_NormalizationTriggered_WhenRankSpaceExhausted()
    {
        // Arrange - 3 siblings with adjacent ranks
        // "a" and "b" are adjacent (no room between), so normalization is triggered
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child1 = new IssueBuilder().WithId("child1")
            .WithParentIssueIdAndOrder("parent1", "a").Build();
        var child2 = new IssueBuilder().WithId("child2")
            .WithParentIssueIdAndOrder("parent1", "b").Build();
        var child3 = new IssueBuilder().WithId("child3")
            .WithParentIssueIdAndOrder("parent1", "c").Build();
        var allIssues = new List<Issue> { parent, child1, child2, child3 };

        // Act
        var (moved, modifiedSiblings) = Dependencies.MoveUp(child3, "parent1", allIssues);

        // Assert - should succeed and normalization should have modified siblings
        moved.Id.Should().Be("child3");
        modifiedSiblings.Should().NotBeEmpty("normalization should have modified siblings");
    }

    #endregion

    #region MoveDown

    [Test]
    public void MoveDown_SuccessfulMoveDown_ReturnsMoved()
    {
        // Arrange - 3 siblings, move the middle one down
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child1 = new IssueBuilder().WithId("child1")
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2")
            .WithParentIssueIdAndOrder("parent1", "bbb").Build();
        var child3 = new IssueBuilder().WithId("child3")
            .WithParentIssueIdAndOrder("parent1", "ccc").Build();
        var allIssues = new List<Issue> { parent, child1, child2, child3 };

        // Act
        var (moved, _) = Dependencies.MoveDown(child2, "parent1", allIssues);

        // Assert - moved issue should have sort order after "ccc"
        moved.Id.Should().Be("child2");
        var sortOrder = moved.ParentIssues
            .First(p => p.ParentIssue == "parent1").SortOrder;
        string.Compare(sortOrder, "ccc", StringComparison.Ordinal)
            .Should().BePositive();
    }

    [Test]
    public void MoveDown_AlreadyAtBottom_ThrowsInvalidOperationException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child1 = new IssueBuilder().WithId("child1")
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2")
            .WithParentIssueIdAndOrder("parent1", "bbb").Build();
        var allIssues = new List<Issue> { parent, child1, child2 };

        // Act
        var act = () => Dependencies.MoveDown(child2, "parent1", allIssues);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already at the bottom*");
    }

    [Test]
    public void MoveDown_NotAChildOfParent_ThrowsInvalidOperationException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child1 = new IssueBuilder().WithId("child1").Build(); // no parent issues
        var allIssues = new List<Issue> { parent, child1 };

        // Act
        var act = () => Dependencies.MoveDown(child1, "parent1", allIssues);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a child*");
    }

    [Test]
    public void MoveDown_NormalizationTriggered_WhenRankSpaceExhausted()
    {
        // Arrange - 3 siblings with adjacent ranks
        // "y" and "z" are adjacent (no room between), so normalization is triggered
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child1 = new IssueBuilder().WithId("child1")
            .WithParentIssueIdAndOrder("parent1", "x").Build();
        var child2 = new IssueBuilder().WithId("child2")
            .WithParentIssueIdAndOrder("parent1", "y").Build();
        var child3 = new IssueBuilder().WithId("child3")
            .WithParentIssueIdAndOrder("parent1", "z").Build();
        var allIssues = new List<Issue> { parent, child1, child2, child3 };

        // Act
        var (moved, modifiedSiblings) = Dependencies.MoveDown(child1, "parent1", allIssues);

        // Assert - should succeed and normalization should have modified siblings
        moved.Id.Should().Be("child1");
        modifiedSiblings.Should().NotBeEmpty("normalization should have modified siblings");
    }

    #endregion
}
