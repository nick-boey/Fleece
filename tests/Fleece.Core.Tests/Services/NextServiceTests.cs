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

    #endregion

    #region PreviousIssues Dependency Tests

    [Test]
    public async Task GetNextIssuesAsync_WithUnmetPreviousIssue_ReturnsEmptyList()
    {
        var blocker = new IssueBuilder().WithId("blocker").WithStatus(IssueStatus.Open).Build();
        var blocked = new IssueBuilder().WithId("blocked").WithStatus(IssueStatus.Open).WithPreviousIssues("blocker").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([blocker, blocked]);

        var result = await _sut.GetNextIssuesAsync();

        // Only blocker should be actionable, blocked is waiting on blocker
        result.Should().ContainSingle().Which.Id.Should().Be("blocker");
    }

    [Test]
    public async Task GetNextIssuesAsync_WithCompletedPreviousIssue_ReturnsBlockedIssue()
    {
        var blocker = new IssueBuilder().WithId("blocker").WithStatus(IssueStatus.Complete).Build();
        var blocked = new IssueBuilder().WithId("blocked").WithStatus(IssueStatus.Open).WithPreviousIssues("blocker").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([blocker, blocked]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().ContainSingle().Which.Id.Should().Be("blocked");
    }

    [Test]
    public async Task GetNextIssuesAsync_WithArchivedPreviousIssue_ReturnsBlockedIssue()
    {
        var blocker = new IssueBuilder().WithId("blocker").WithStatus(IssueStatus.Archived).Build();
        var blocked = new IssueBuilder().WithId("blocked").WithStatus(IssueStatus.Open).WithPreviousIssues("blocker").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([blocker, blocked]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().ContainSingle().Which.Id.Should().Be("blocked");
    }

    [Test]
    public async Task GetNextIssuesAsync_WithClosedPreviousIssue_ReturnsBlockedIssue()
    {
        var blocker = new IssueBuilder().WithId("blocker").WithStatus(IssueStatus.Closed).Build();
        var blocked = new IssueBuilder().WithId("blocked").WithStatus(IssueStatus.Open).WithPreviousIssues("blocker").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([blocker, blocked]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().ContainSingle().Which.Id.Should().Be("blocked");
    }

    [Test]
    public async Task GetNextIssuesAsync_WithMultiplePreviousIssues_AllMustBeComplete()
    {
        var blocker1 = new IssueBuilder().WithId("blocker1").WithStatus(IssueStatus.Complete).Build();
        var blocker2 = new IssueBuilder().WithId("blocker2").WithStatus(IssueStatus.Open).Build();
        var blocked = new IssueBuilder().WithId("blocked").WithStatus(IssueStatus.Open).WithPreviousIssues("blocker1", "blocker2").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([blocker1, blocker2, blocked]);

        var result = await _sut.GetNextIssuesAsync();

        // blocker2 is still open, so blocked is not actionable
        result.Should().ContainSingle().Which.Id.Should().Be("blocker2");
    }

    [Test]
    public async Task GetNextIssuesAsync_WithNonExistentPreviousIssue_TreatsAsComplete()
    {
        // If a previous issue doesn't exist in the system, treat it as done
        var blocked = new IssueBuilder().WithId("blocked").WithStatus(IssueStatus.Open).WithPreviousIssues("nonexistent").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([blocked]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().ContainSingle().Which.Id.Should().Be("blocked");
    }

    #endregion

    #region Series Execution Mode Tests

    [Test]
    public async Task GetNextIssuesAsync_SeriesParent_OnlyFirstChildIsActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open).WithParentIssues("parent").WithPriority(1).Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open).WithParentIssues("parent").WithPriority(2).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        // In series mode, only the first child (by priority) should be actionable
        result.Select(i => i.Id).Should().BeEquivalentTo(["parent", "child1"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_SeriesParent_SecondChildActionableWhenFirstComplete()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Complete).WithParentIssues("parent").WithPriority(1).Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open).WithParentIssues("parent").WithPriority(2).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        result.Select(i => i.Id).Should().BeEquivalentTo(["parent", "child2"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_SeriesParent_SortsByPriorityThenTitle()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var childA = new IssueBuilder().WithId("childA").WithTitle("A Task").WithStatus(IssueStatus.Open).WithParentIssues("parent").Build();
        var childB = new IssueBuilder().WithId("childB").WithTitle("B Task").WithStatus(IssueStatus.Open).WithParentIssues("parent").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, childB, childA]);

        var result = await _sut.GetNextIssuesAsync();

        // Without priority, should sort by title - childA comes first
        result.Select(i => i.Id).Should().BeEquivalentTo(["parent", "childA"]);
    }

    #endregion

    #region Parallel Execution Mode Tests

    [Test]
    public async Task GetNextIssuesAsync_ParallelParent_AllChildrenActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open).WithParentIssues("parent").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open).WithParentIssues("parent").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        result.Select(i => i.Id).Should().BeEquivalentTo(["parent", "child1", "child2"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_ParallelParent_ChildWithBlockedDependencyNotActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var blocker = new IssueBuilder().WithId("blocker").WithStatus(IssueStatus.Open).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open).WithParentIssues("parent").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open).WithParentIssues("parent").WithPreviousIssues("blocker").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, blocker, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        // child2 blocked by blocker, but child1 is still actionable
        result.Select(i => i.Id).Should().BeEquivalentTo(["parent", "blocker", "child1"]);
    }

    #endregion

    #region Nested Hierarchy Tests

    [Test]
    public async Task GetNextIssuesAsync_NestedHierarchy_RespectsParentExecutionModes()
    {
        var grandparent = new IssueBuilder().WithId("grandparent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var parent1 = new IssueBuilder().WithId("parent1").WithStatus(IssueStatus.Open).WithParentIssues("grandparent").WithExecutionMode(ExecutionMode.Series).Build();
        var parent2 = new IssueBuilder().WithId("parent2").WithStatus(IssueStatus.Open).WithParentIssues("grandparent").WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1a = new IssueBuilder().WithId("child1a").WithStatus(IssueStatus.Open).WithParentIssues("parent1").WithPriority(1).Build();
        var child1b = new IssueBuilder().WithId("child1b").WithStatus(IssueStatus.Open).WithParentIssues("parent1").WithPriority(2).Build();
        var child2a = new IssueBuilder().WithId("child2a").WithStatus(IssueStatus.Open).WithParentIssues("parent2").Build();
        var child2b = new IssueBuilder().WithId("child2b").WithStatus(IssueStatus.Open).WithParentIssues("parent2").Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([grandparent, parent1, parent2, child1a, child1b, child2a, child2b]);

        var result = await _sut.GetNextIssuesAsync();

        // grandparent is parallel, so parent1 and parent2 are both actionable
        // parent1 is series, so only child1a is actionable (not child1b)
        // parent2 is parallel, so both child2a and child2b are actionable
        result.Select(i => i.Id).Should().BeEquivalentTo(["grandparent", "parent1", "parent2", "child1a", "child2a", "child2b"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_OrphanChild_IsActionable()
    {
        // If a child's parent doesn't exist, the child should still be actionable
        var orphan = new IssueBuilder().WithId("orphan").WithStatus(IssueStatus.Open).WithParentIssues("nonexistent").Build();
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
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open).WithParentIssues("parent").Build();
        var other = new IssueBuilder().WithId("other").WithStatus(IssueStatus.Open).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, other]);

        var result = await _sut.GetNextIssuesAsync(parentId: "parent");

        result.Select(i => i.Id).Should().BeEquivalentTo(["child1"]);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task GetNextIssuesAsync_MultipleParents_AllParentsMustAllowIt()
    {
        // Issue with multiple parents - must pass series check for all parents
        var parent1 = new IssueBuilder().WithId("parent1").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var parent2 = new IssueBuilder().WithId("parent2").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var sibling = new IssueBuilder().WithId("sibling").WithStatus(IssueStatus.Open).WithParentIssues("parent1").WithPriority(1).Build();
        var multiParent = new IssueBuilder().WithId("multiParent").WithStatus(IssueStatus.Open).WithParentIssues("parent1", "parent2").WithPriority(2).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent1, parent2, sibling, multiParent]);

        var result = await _sut.GetNextIssuesAsync();

        // multiParent is blocked by sibling under parent1 (series mode)
        result.Select(i => i.Id).Should().BeEquivalentTo(["parent1", "parent2", "sibling"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_DefaultExecutionModeIsSeries()
    {
        // Without explicit ExecutionMode, default should be Series
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).Build(); // No ExecutionMode set
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open).WithParentIssues("parent").WithPriority(1).Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open).WithParentIssues("parent").WithPriority(2).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        // Default is Series, so only child1 should be actionable
        result.Select(i => i.Id).Should().BeEquivalentTo(["parent", "child1"]);
    }

    #endregion
}
