using Fleece.Core.Models;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class DependencyServiceTests
{
    private IIssueService _issueService = null!;
    private IValidationService _validationService = null!;
    private DependencyService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _issueService = Substitute.For<IIssueService>();
        _validationService = Substitute.For<IValidationService>();
        _sut = new DependencyService(_issueService, _validationService);
    }

    #region AddDependencyAsync

    [Test]
    public async Task AddDependencyAsync_SuccessfulAdd_ReturnsUpdatedIssue()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1").Build();

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent1", "child1", Arg.Any<CancellationToken>())
            .Returns(false);
        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent, child });

        var updatedChild = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act
        var result = await _sut.AddDependencyAsync("parent1", "child1");

        // Assert
        result.Id.Should().Be("child1");
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p =>
                p.Count == 1 && p[0].ParentIssue == "parent1"),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddDependencyAsync_DuplicateRelationship_ThrowsInvalidOperationException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1").WithParentIssueIds("parent1").Build();

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });

        // Act
        var act = () => _sut.AddDependencyAsync("parent1", "child1");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already a child*");
    }

    [Test]
    public async Task AddDependencyAsync_CycleDetected_ThrowsInvalidOperationException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1").Build();

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent1", "child1", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var act = () => _sut.AddDependencyAsync("parent1", "child1");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*circular dependency*");
    }

    [Test]
    public async Task AddDependencyAsync_ParentNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _issueService.ResolveByPartialIdAsync("nonexist", Arg.Any<CancellationToken>())
            .Returns(new List<Issue>());

        // Act
        var act = () => _sut.AddDependencyAsync("nonexist", "child1");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*parent*nonexist*");
    }

    [Test]
    public async Task AddDependencyAsync_ChildNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("nonexist", Arg.Any<CancellationToken>())
            .Returns(new List<Issue>());

        // Act
        var act = () => _sut.AddDependencyAsync("parent1", "nonexist");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*child*nonexist*");
    }

    [Test]
    public async Task AddDependencyAsync_AmbiguousId_ThrowsInvalidOperationException()
    {
        // Arrange
        var match1 = new IssueBuilder().WithId("abc123").Build();
        var match2 = new IssueBuilder().WithId("abc456").Build();
        _issueService.ResolveByPartialIdAsync("abc", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { match1, match2 });

        // Act
        var act = () => _sut.AddDependencyAsync("abc", "child1");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Multiple issues match*");
    }

    [Test]
    public async Task AddDependencyAsync_DefaultPosition_AppendsAtEnd()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var existingChild = new IssueBuilder().WithId("existing1")
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var child = new IssueBuilder().WithId("child1").Build();

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent1", "child1", Arg.Any<CancellationToken>())
            .Returns(false);
        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent, existingChild, child });

        var updatedChild = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act
        await _sut.AddDependencyAsync("parent1", "child1");

        // Assert - sort order should be after "aaa"
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p =>
                p.Count == 1 &&
                p[0].ParentIssue == "parent1" &&
                string.Compare(p[0].SortOrder, "aaa", StringComparison.Ordinal) > 0),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddDependencyAsync_FirstPosition_PrependsAtBeginning()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var existingChild = new IssueBuilder().WithId("existing1")
            .WithParentIssueIdAndOrder("parent1", "nnn").Build();
        var child = new IssueBuilder().WithId("child1").Build();

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent1", "child1", Arg.Any<CancellationToken>())
            .Returns(false);
        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent, existingChild, child });

        var updatedChild = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act
        await _sut.AddDependencyAsync("parent1", "child1",
            new DependencyPosition { Kind = DependencyPositionKind.First });

        // Assert - sort order should be before "nnn"
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p =>
                p.Count == 1 &&
                p[0].ParentIssue == "parent1" &&
                string.Compare(p[0].SortOrder, "nnn", StringComparison.Ordinal) < 0),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddDependencyAsync_AfterPosition_PlacesAfterSibling()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var sibling1 = new IssueBuilder().WithId("sibling1")
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var sibling2 = new IssueBuilder().WithId("sibling2")
            .WithParentIssueIdAndOrder("parent1", "nnn").Build();
        var child = new IssueBuilder().WithId("child1").Build();

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent1", "child1", Arg.Any<CancellationToken>())
            .Returns(false);
        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent, sibling1, sibling2, child });

        var updatedChild = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act
        await _sut.AddDependencyAsync("parent1", "child1",
            new DependencyPosition { Kind = DependencyPositionKind.After, SiblingId = "sibling1" });

        // Assert - sort order should be between "aaa" and "nnn"
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p =>
                p.Count == 1 &&
                p[0].ParentIssue == "parent1" &&
                string.Compare(p[0].SortOrder, "aaa", StringComparison.Ordinal) > 0 &&
                string.Compare(p[0].SortOrder, "nnn", StringComparison.Ordinal) < 0),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddDependencyAsync_BeforePosition_PlacesBeforeSibling()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var sibling1 = new IssueBuilder().WithId("sibling1")
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var sibling2 = new IssueBuilder().WithId("sibling2")
            .WithParentIssueIdAndOrder("parent1", "nnn").Build();
        var child = new IssueBuilder().WithId("child1").Build();

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent1", "child1", Arg.Any<CancellationToken>())
            .Returns(false);
        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent, sibling1, sibling2, child });

        var updatedChild = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act
        await _sut.AddDependencyAsync("parent1", "child1",
            new DependencyPosition { Kind = DependencyPositionKind.Before, SiblingId = "sibling2" });

        // Assert - sort order should be between "aaa" and "nnn"
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p =>
                p.Count == 1 &&
                p[0].ParentIssue == "parent1" &&
                string.Compare(p[0].SortOrder, "aaa", StringComparison.Ordinal) > 0 &&
                string.Compare(p[0].SortOrder, "nnn", StringComparison.Ordinal) < 0),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddDependencyAsync_AfterPosition_SiblingNotChildOfParent_ThrowsInvalidOperationException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1").Build();
        var notASibling = new IssueBuilder().WithId("notsibling").Build();

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent1", "child1", Arg.Any<CancellationToken>())
            .Returns(false);
        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent, child, notASibling });

        // Act
        var act = () => _sut.AddDependencyAsync("parent1", "child1",
            new DependencyPosition { Kind = DependencyPositionKind.After, SiblingId = "notsibling" });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a child*");
    }

    #endregion

    #region AddDependencyAsync_ReplaceExisting

    [Test]
    public async Task AddDependencyAsync_ReplaceExisting_True_ReplacesAllParents()
    {
        // Arrange
        var parent1 = new IssueBuilder().WithId("parent1").Build();
        var parent2 = new IssueBuilder().WithId("parent2").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();

        _issueService.ResolveByPartialIdAsync("parent2", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent2 });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent2", "child1", Arg.Any<CancellationToken>())
            .Returns(false);
        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent1, parent2, child });

        var updatedChild = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent2").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act
        var result = await _sut.AddDependencyAsync("parent2", "child1", replaceExisting: true);

        // Assert - should only have parent2, not parent1
        result.Id.Should().Be("child1");
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p =>
                p.Count == 1 && p[0].ParentIssue == "parent2"),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddDependencyAsync_ReplaceExisting_False_PreservesExistingParents()
    {
        // Arrange
        var parent1 = new IssueBuilder().WithId("parent1").Build();
        var parent2 = new IssueBuilder().WithId("parent2").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();

        _issueService.ResolveByPartialIdAsync("parent2", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent2 });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent2", "child1", Arg.Any<CancellationToken>())
            .Returns(false);
        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent1, parent2, child });

        var updatedChild = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1", "parent2").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act
        var result = await _sut.AddDependencyAsync("parent2", "child1", replaceExisting: false);

        // Assert - should have both parent1 and parent2
        result.Id.Should().Be("child1");
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p =>
                p.Count == 2 &&
                p[0].ParentIssue == "parent1" &&
                p[1].ParentIssue == "parent2"),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddDependencyAsync_ReplaceExisting_AllowsSameParent()
    {
        // Arrange - child already has parent1, re-adding with replace should succeed
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent1", "child1", Arg.Any<CancellationToken>())
            .Returns(false);
        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent, child });

        var updatedChild = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act - should not throw even though relationship exists
        var result = await _sut.AddDependencyAsync("parent1", "child1", replaceExisting: true);

        // Assert
        result.Id.Should().Be("child1");
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p =>
                p.Count == 1 && p[0].ParentIssue == "parent1"),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddDependencyAsync_NotReplacing_DuplicateRelationship_ThrowsInvalidOperationException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1").WithParentIssueIds("parent1").Build();

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });

        // Act - replaceExisting defaults to false
        var act = () => _sut.AddDependencyAsync("parent1", "child1", replaceExisting: false);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already a child*");
    }

    #endregion

    #region AddDependencyAsync_MakePrimary

    [Test]
    public async Task AddDependencyAsync_MakePrimary_True_InsertsAtFront()
    {
        // Arrange
        var parent1 = new IssueBuilder().WithId("parent1").Build();
        var parent2 = new IssueBuilder().WithId("parent2").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();

        _issueService.ResolveByPartialIdAsync("parent2", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent2 });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent2", "child1", Arg.Any<CancellationToken>())
            .Returns(false);
        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent1, parent2, child });

        var updatedChild = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent2", "parent1").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act
        var result = await _sut.AddDependencyAsync("parent2", "child1", makePrimary: true);

        // Assert - parent2 should be first (primary), then parent1
        result.Id.Should().Be("child1");
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p =>
                p.Count == 2 &&
                p[0].ParentIssue == "parent2" &&
                p[1].ParentIssue == "parent1"),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddDependencyAsync_MakePrimary_False_AppendsAtEnd()
    {
        // Arrange
        var parent1 = new IssueBuilder().WithId("parent1").Build();
        var parent2 = new IssueBuilder().WithId("parent2").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();

        _issueService.ResolveByPartialIdAsync("parent2", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent2 });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent2", "child1", Arg.Any<CancellationToken>())
            .Returns(false);
        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent1, parent2, child });

        var updatedChild = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1", "parent2").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act
        var result = await _sut.AddDependencyAsync("parent2", "child1", makePrimary: false);

        // Assert - parent1 should be first (primary), then parent2
        result.Id.Should().Be("child1");
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p =>
                p.Count == 2 &&
                p[0].ParentIssue == "parent1" &&
                p[1].ParentIssue == "parent2"),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddDependencyAsync_ReplaceAndMakePrimary_Together()
    {
        // Arrange - child has parent1, replace with parent2 as primary
        var parent1 = new IssueBuilder().WithId("parent1").Build();
        var parent2 = new IssueBuilder().WithId("parent2").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();

        _issueService.ResolveByPartialIdAsync("parent2", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent2 });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _validationService.WouldCreateCycleAsync("parent2", "child1", Arg.Any<CancellationToken>())
            .Returns(false);
        _issueService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent1, parent2, child });

        var updatedChild = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent2").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act
        var result = await _sut.AddDependencyAsync("parent2", "child1",
            replaceExisting: true, makePrimary: true);

        // Assert - should only have parent2 (parent1 replaced)
        result.Id.Should().Be("child1");
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p =>
                p.Count == 1 && p[0].ParentIssue == "parent2"),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    #endregion

    #region RemoveDependencyAsync

    [Test]
    public async Task RemoveDependencyAsync_SuccessfulRemove_ReturnsUpdatedIssue()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1").Build();

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });

        var updatedChild = new IssueBuilder().WithId("child1").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act
        var result = await _sut.RemoveDependencyAsync("parent1", "child1");

        // Assert
        result.Id.Should().Be("child1");
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p => p.Count == 0),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveDependencyAsync_RelationshipDoesNotExist_ThrowsInvalidOperationException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        var child = new IssueBuilder().WithId("child1").Build(); // no parent issues

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });

        // Act
        var act = () => _sut.RemoveDependencyAsync("parent1", "child1");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a child*");
    }

    [Test]
    public async Task RemoveDependencyAsync_ParentNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _issueService.ResolveByPartialIdAsync("nonexist", Arg.Any<CancellationToken>())
            .Returns(new List<Issue>());

        // Act
        var act = () => _sut.RemoveDependencyAsync("nonexist", "child1");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*parent*nonexist*");
    }

    [Test]
    public async Task RemoveDependencyAsync_ChildNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var parent = new IssueBuilder().WithId("parent1").Build();
        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _issueService.ResolveByPartialIdAsync("nonexist", Arg.Any<CancellationToken>())
            .Returns(new List<Issue>());

        // Act
        var act = () => _sut.RemoveDependencyAsync("parent1", "nonexist");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*child*nonexist*");
    }

    [Test]
    public async Task RemoveDependencyAsync_PreservesOtherParents()
    {
        // Arrange
        var parent1 = new IssueBuilder().WithId("parent1").Build();
        var parent2 = new IssueBuilder().WithId("parent2").Build();
        var child = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent1", "parent2").Build();

        _issueService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent1 });
        _issueService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });

        var updatedChild = new IssueBuilder().WithId("child1")
            .WithParentIssueIds("parent2").Build();
        _issueService.UpdateAsync(
                id: "child1",
                parentIssues: Arg.Any<IReadOnlyList<ParentIssueRef>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(updatedChild);

        // Act
        await _sut.RemoveDependencyAsync("parent1", "child1");

        // Assert - should still have parent2
        await _issueService.Received(1).UpdateAsync(
            id: "child1",
            parentIssues: Arg.Is<IReadOnlyList<ParentIssueRef>>(p =>
                p.Count == 1 && p[0].ParentIssue == "parent2"),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    #endregion
}
