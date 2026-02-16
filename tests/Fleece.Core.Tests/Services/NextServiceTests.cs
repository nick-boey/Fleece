using Fleece.Core.Models;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class NextServiceTests
{
    private IIssueService _issueService = null!;
    private NextService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _issueService = Substitute.For<IIssueService>();
        _sut = new NextService(_issueService);
    }

    #region Basic Actionability Tests

    [Test]
    public async Task GetNextIssuesAsync_WithNoIssues_ReturnsEmptyList()
    {
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetNextIssuesAsync_WithSingleOpenIssue_ReturnsIssue()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Open).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().ContainSingle().Which.Id.Should().Be("issue1");
    }

    [Test]
    public async Task GetNextIssuesAsync_WithClosedIssue_ReturnsEmptyList()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Closed).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetNextIssuesAsync_WithProgressIssue_ReturnsEmptyList()
    {
        // Issues in Progress are not actionable for "next" - they're already being worked on
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Progress).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetNextIssuesAsync_WithCompletedIssue_ReturnsEmptyList()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Complete).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetNextIssuesAsync_WithArchivedIssue_ReturnsEmptyList()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Archived).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().BeEmpty();
    }

    #endregion

    #region Series Execution Mode Tests

    [Test]
    public async Task GetNextIssuesAsync_SeriesParent_OnlyFirstChildIsActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        // In series mode, only the first child (by sort order) should be actionable
        // Parent is NOT actionable while it has incomplete children
        result.Select(i => i.Id).Should().BeEquivalentTo(["child1"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_SeriesParent_SecondChildActionableWhenFirstComplete()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Complete).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        // Parent is NOT actionable while it has incomplete children
        result.Select(i => i.Id).Should().BeEquivalentTo(["child2"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_SeriesParent_SortsBySortOrder()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        // childB has lower sort order so should come first despite being added second
        var childA = new IssueBuilder().WithId("childA").WithTitle("A Task").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();
        var childB = new IssueBuilder().WithId("childB").WithTitle("B Task").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, childA, childB]);

        var result = await _sut.GetNextIssuesAsync();

        // childB should be actionable (lower sort order)
        // Parent is NOT actionable while it has incomplete children
        result.Select(i => i.Id).Should().BeEquivalentTo(["childB"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_SeriesParent_ThirdChildActionableWhenFirstTwoComplete()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Complete).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Closed).WithParentIssueIdAndOrder("parent", "bbb").Build();
        var child3 = new IssueBuilder().WithId("child3").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "ccc").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2, child3]);

        var result = await _sut.GetNextIssuesAsync();

        // Parent is NOT actionable while it has incomplete children
        result.Select(i => i.Id).Should().BeEquivalentTo(["child3"]);
    }

    #endregion

    #region Parallel Execution Mode Tests

    [Test]
    public async Task GetNextIssuesAsync_ParallelParent_AllChildrenActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        // Parent is NOT actionable while it has incomplete children
        result.Select(i => i.Id).Should().BeEquivalentTo(["child1", "child2"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_ParallelParent_RemainingChildrenActionableWhenSomeComplete()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Complete).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();
        var child3 = new IssueBuilder().WithId("child3").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "ccc").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2, child3]);

        var result = await _sut.GetNextIssuesAsync();

        // child1 is complete, but child2 and child3 are still actionable
        // Parent is NOT actionable while it has incomplete children
        result.Select(i => i.Id).Should().BeEquivalentTo(["child2", "child3"]);
    }

    #endregion

    #region Nested Hierarchy Tests

    [Test]
    public async Task GetNextIssuesAsync_NestedHierarchy_RespectsParentExecutionModes()
    {
        var grandparent = new IssueBuilder().WithId("grandparent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var parent1 = new IssueBuilder().WithId("parent1").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("grandparent", "aaa").WithExecutionMode(ExecutionMode.Series).Build();
        var parent2 = new IssueBuilder().WithId("parent2").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("grandparent", "bbb").WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1a = new IssueBuilder().WithId("child1a").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var child1b = new IssueBuilder().WithId("child1b").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent1", "bbb").Build();
        var child2a = new IssueBuilder().WithId("child2a").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent2", "aaa").Build();
        var child2b = new IssueBuilder().WithId("child2b").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent2", "bbb").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([grandparent, parent1, parent2, child1a, child1b, child2a, child2b]);

        var result = await _sut.GetNextIssuesAsync();

        // All parents have incomplete children, so only leaf actionable issues are returned
        // parent1 is series, so only child1a is actionable (not child1b)
        // parent2 is parallel, so both child2a and child2b are actionable
        result.Select(i => i.Id).Should().BeEquivalentTo(["child1a", "child2a", "child2b"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_OrphanChild_IsActionable()
    {
        // If a child's parent doesn't exist, the child should still be actionable
        var orphan = new IssueBuilder().WithId("orphan").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("nonexistent", "aaa").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([orphan]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().ContainSingle().Which.Id.Should().Be("orphan");
    }

    #endregion

    #region Parent Filter Tests

    [Test]
    public async Task GetNextIssuesAsync_WithParentFilter_ReturnsOnlyDescendants()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var other = new IssueBuilder().WithId("other").WithStatus(IssueStatus.Open).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, other]);

        var result = await _sut.GetNextIssuesAsync(parentId: "parent");

        result.Select(i => i.Id).Should().BeEquivalentTo(["child1"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_WithParentFilter_IncludesNestedDescendants()
    {
        var grandparent = new IssueBuilder().WithId("grandparent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("grandparent", "aaa").WithExecutionMode(ExecutionMode.Parallel).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var other = new IssueBuilder().WithId("other").WithStatus(IssueStatus.Open).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([grandparent, parent, child, other]);

        var result = await _sut.GetNextIssuesAsync(parentId: "grandparent");

        // Parent has incomplete child, so only leaf descendants are returned
        result.Select(i => i.Id).Should().BeEquivalentTo(["child"]);
    }

    #endregion

    #region Parent With Children Tests

    [Test]
    public async Task GetNextIssuesAsync_ParentWithOpenChildren_IsNotActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        // Parent should NOT be actionable while it has open children
        result.Select(i => i.Id).Should().BeEquivalentTo(["child1", "child2"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_ParentWithAllChildrenDone_IsActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Complete).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Closed).WithParentIssueIdAndOrder("parent", "bbb").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        // Parent IS actionable when all children are done
        result.Select(i => i.Id).Should().BeEquivalentTo(["parent"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_ParentWithMixOfDoneAndOpenChildren_IsNotActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Complete).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        // Parent should NOT be actionable - child2 is still open
        result.Select(i => i.Id).Should().BeEquivalentTo(["child2"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_ParentWithNoChildren_IsActionable()
    {
        // An issue with ExecutionMode set but no children is still actionable
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent]);

        var result = await _sut.GetNextIssuesAsync();

        result.Select(i => i.Id).Should().BeEquivalentTo(["parent"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_SeriesParent_SameSortOrderBreaksTieByPriority()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        // Both children have same sort order, but child2 has higher priority (lower number)
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Zebra").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").WithPriority(3).Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Alpha").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").WithPriority(1).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        // child2 should be first (higher priority = lower number)
        result.Select(i => i.Id).Should().BeEquivalentTo(["child2"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_SeriesParent_SameSortOrderAndPriorityBreaksTieByTitle()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Zebra Task").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").WithPriority(2).Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Alpha Task").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").WithPriority(2).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        // child2 should be first (alphabetically earlier title)
        result.Select(i => i.Id).Should().BeEquivalentTo(["child2"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_DeepSeriesChain_OnlyDeepestLeafIsActionable()
    {
        // Simulates: parent (series) -> child with subtree vs leaf sibling
        // Only the deepest leaf in the first subtree should be actionable
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var subtreeChild = new IssueBuilder().WithId("subtreeChild").WithTitle("A - Subtree").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").WithPriority(2).Build();
        var leafSibling = new IssueBuilder().WithId("leafSibling").WithTitle("B - Leaf").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").WithPriority(3).Build();
        var deepLeaf = new IssueBuilder().WithId("deepLeaf").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("subtreeChild", "aaa").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, subtreeChild, leafSibling, deepLeaf]);

        var result = await _sut.GetNextIssuesAsync();

        // subtreeChild is first child (higher priority) but has incomplete children -> not actionable
        // leafSibling is blocked because subtreeChild comes first in series
        // deepLeaf is the only actionable issue
        result.Select(i => i.Id).Should().BeEquivalentTo(["deepLeaf"]);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task GetNextIssuesAsync_MultipleParents_AllParentsMustAllowIt()
    {
        // Issue with multiple parents - must pass series check for all parents
        var parent1 = new IssueBuilder().WithId("parent1").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var parent2 = new IssueBuilder().WithId("parent2").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var sibling = new IssueBuilder().WithId("sibling").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var multiParent = new IssueBuilder()
            .WithId("multiParent")
            .WithStatus(IssueStatus.Open)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "bbb" },
                new ParentIssueRef { ParentIssue = "parent2", SortOrder = "aaa" })
            .Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent1, parent2, sibling, multiParent]);

        var result = await _sut.GetNextIssuesAsync();

        // multiParent is blocked by sibling under parent1 (series mode)
        // Both parents have incomplete children, so they are NOT actionable
        result.Select(i => i.Id).Should().BeEquivalentTo(["sibling"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_DefaultExecutionModeIsSeries()
    {
        // Without explicit ExecutionMode, default should be Series
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).Build(); // No ExecutionMode set
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        // Default is Series, so only child1 should be actionable
        // Parent is NOT actionable while it has incomplete children
        result.Select(i => i.Id).Should().BeEquivalentTo(["child1"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_MultipleOpenIssuesWithNoParent_AllAreActionable()
    {
        var issue1 = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Open).Build();
        var issue2 = new IssueBuilder().WithId("issue2").WithStatus(IssueStatus.Open).Build();
        var issue3 = new IssueBuilder().WithId("issue3").WithStatus(IssueStatus.Open).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([issue1, issue2, issue3]);

        var result = await _sut.GetNextIssuesAsync();

        result.Select(i => i.Id).Should().BeEquivalentTo(["issue1", "issue2", "issue3"]);
    }

    #endregion
}
