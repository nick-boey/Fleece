using Fleece.Core.Models;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

/// <summary>
/// Tests for graph methods (BuildGraphAsync, QueryGraphAsync, GetNextIssuesAsync, BuildTaskGraphLayoutAsync)
/// which are now part of IssueService.
/// </summary>
[TestFixture]
public class IssueServiceGraphTests
{
    private IStorageService _storageService = null!;
    private IssueService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _storageService = Substitute.For<IStorageService>();
        var idGenerator = Substitute.For<IIdGenerator>();
        var gitConfigService = Substitute.For<IGitConfigService>();
        var tagService = new TagService();
        _sut = new IssueService(_storageService, idGenerator, gitConfigService, tagService);
    }

    #region BuildGraphAsync Basic Tests

    [Test]
    public async Task BuildGraphAsync_WithNoIssues_ReturnsEmptyGraph()
    {
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes.Should().BeEmpty();
        result.RootIssueIds.Should().BeEmpty();
    }

    [Test]
    public async Task BuildGraphAsync_WithSingleIssue_ReturnsOneNode()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Open).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes.Should().ContainSingle();
        result.Nodes["issue1"].Issue.Id.Should().Be("issue1");
        result.RootIssueIds.Should().ContainSingle().Which.Should().Be("issue1");
    }

    [Test]
    public async Task BuildGraphAsync_ParentChildRelationship_CorrectChildAndParentIds()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes.Should().HaveCount(2);

        // Parent node
        var parentNode = result.Nodes["parent"];
        parentNode.ChildIssueIds.Should().ContainSingle().Which.Should().Be("child");
        parentNode.ParentIssueIds.Should().BeEmpty();

        // Child node
        var childNode = result.Nodes["child"];
        childNode.ChildIssueIds.Should().BeEmpty();
        childNode.ParentIssueIds.Should().ContainSingle().Which.Should().Be("parent");

        // Root IDs
        result.RootIssueIds.Should().ContainSingle().Which.Should().Be("parent");
    }

    #endregion

    #region Next/Previous Computation - Series Mode

    [Test]
    public async Task BuildGraphAsync_SeriesParent_FirstChildHasNoPrevious()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.BuildGraphAsync();

        var child1Node = result.Nodes["child1"];
        child1Node.PreviousIssueIds.Should().BeEmpty();
        child1Node.NextIssueIds.Should().ContainSingle().Which.Should().Be("child2");
    }

    [Test]
    public async Task BuildGraphAsync_SeriesParent_MiddleChildHasBothPreviousAndNext()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        var child3 = new IssueBuilder().WithId("child3").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "ccc").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2, child3]);

        var result = await _sut.BuildGraphAsync();

        var child2Node = result.Nodes["child2"];
        child2Node.PreviousIssueIds.Should().ContainSingle().Which.Should().Be("child1");
        child2Node.NextIssueIds.Should().ContainSingle().Which.Should().Be("child3");
    }

    [Test]
    public async Task BuildGraphAsync_SeriesParent_LastChildHasNoNext()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.BuildGraphAsync();

        var child2Node = result.Nodes["child2"];
        child2Node.PreviousIssueIds.Should().ContainSingle().Which.Should().Be("child1");
        child2Node.NextIssueIds.Should().BeEmpty();
    }

    #endregion

    #region Next/Previous Computation - Parallel Mode

    [Test]
    public async Task BuildGraphAsync_ParallelParent_AllChildrenHaveEmptyPreviousAndNext()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes["child1"].PreviousIssueIds.Should().BeEmpty();
        result.Nodes["child1"].NextIssueIds.Should().BeEmpty();
        result.Nodes["child2"].PreviousIssueIds.Should().BeEmpty();
        result.Nodes["child2"].NextIssueIds.Should().BeEmpty();
    }

    #endregion

    #region Next/Previous Computation - Root Issues

    [Test]
    public async Task BuildGraphAsync_RootIssues_HaveEmptyPreviousAndNext()
    {
        var issue1 = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Open).Build();
        var issue2 = new IssueBuilder().WithId("issue2").WithStatus(IssueStatus.Open).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([issue1, issue2]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes["issue1"].PreviousIssueIds.Should().BeEmpty();
        result.Nodes["issue1"].NextIssueIds.Should().BeEmpty();
        result.Nodes["issue2"].PreviousIssueIds.Should().BeEmpty();
        result.Nodes["issue2"].NextIssueIds.Should().BeEmpty();
    }

    #endregion

    #region Next/Previous Computation - Multi-Parent (DAG)

    [Test]
    public async Task BuildGraphAsync_MultipleSeriesParents_AccumulatesPreviousFromAll()
    {
        var parent1 = new IssueBuilder().WithId("parent1").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var parent2 = new IssueBuilder().WithId("parent2").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var sibling1 = new IssueBuilder().WithId("sibling1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var multiParent = new IssueBuilder()
            .WithId("multiParent")
            .WithStatus(IssueStatus.Open)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "bbb" },
                new ParentIssueRef { ParentIssue = "parent2", SortOrder = "aaa" })
            .Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent1, parent2, sibling1, multiParent]);

        var result = await _sut.BuildGraphAsync();

        // multiParent should have sibling1 as previous (from parent1's series)
        var multiParentNode = result.Nodes["multiParent"];
        multiParentNode.PreviousIssueIds.Should().Contain("sibling1");
    }

    #endregion

    #region HasIncompleteChildren / AllPreviousDone

    [Test]
    public async Task BuildGraphAsync_ParentWithOpenChildren_HasIncompleteChildrenIsTrue()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes["parent"].HasIncompleteChildren.Should().BeTrue();
        result.Nodes["child"].HasIncompleteChildren.Should().BeFalse();
    }

    [Test]
    public async Task BuildGraphAsync_ParentWithAllChildrenDone_HasIncompleteChildrenIsFalse()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Complete)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes["parent"].HasIncompleteChildren.Should().BeFalse();
    }

    [Test]
    public async Task BuildGraphAsync_SeriesChild_AllPreviousDoneWhenPrevComplete()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Complete)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes["child2"].AllPreviousDone.Should().BeTrue();
    }

    [Test]
    public async Task BuildGraphAsync_SeriesChild_AllPreviousDoneFalseWhenPrevOpen()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes["child2"].AllPreviousDone.Should().BeFalse();
    }

    #endregion

    #region GetNextIssuesAsync Tests (ported from NextServiceTests)

    [Test]
    public async Task GetNextIssuesAsync_WithNoIssues_ReturnsEmptyList()
    {
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetNextIssuesAsync_WithSingleOpenIssue_ReturnsIssue()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Open).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().ContainSingle().Which.Id.Should().Be("issue1");
    }

    [Test]
    public async Task GetNextIssuesAsync_WithClosedIssue_ReturnsEmptyList()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Closed).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetNextIssuesAsync_WithProgressIssue_ReturnsEmptyList()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Progress).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetNextIssuesAsync_WithReviewIssue_ReturnsIssue()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Review).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().ContainSingle().Which.Id.Should().Be("issue1");
    }

    [Test]
    public async Task GetNextIssuesAsync_WithDraftIssue_ReturnsEmptyList()
    {
        // Draft issues are not actionable - they need to be fully specified first
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Draft).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetNextIssuesAsync_SeriesParent_OnlyFirstChildIsActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        result.Select(i => i.Id).Should().BeEquivalentTo(["child1"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_SeriesParent_SecondChildActionableWhenFirstComplete()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Complete)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        result.Select(i => i.Id).Should().BeEquivalentTo(["child2"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_ParallelParent_AllChildrenActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.GetNextIssuesAsync();

        result.Select(i => i.Id).Should().BeEquivalentTo(["child1", "child2"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_ParentWithIncompleteChildren_IsNotActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1]);

        var result = await _sut.GetNextIssuesAsync();

        result.Select(i => i.Id).Should().BeEquivalentTo(["child1"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_ParentWithAllChildrenDone_IsActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Complete)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1]);

        var result = await _sut.GetNextIssuesAsync();

        result.Select(i => i.Id).Should().BeEquivalentTo(["parent"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_WithParentFilter_ReturnsOnlyDescendants()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var other = new IssueBuilder().WithId("other").WithStatus(IssueStatus.Open).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, other]);

        var result = await _sut.GetNextIssuesAsync(parentId: "parent");

        result.Select(i => i.Id).Should().BeEquivalentTo(["child1"]);
    }

    [Test]
    public async Task GetNextIssuesAsync_SortsReviewStatusBeforeOpen()
    {
        var openIssue = new IssueBuilder().WithId("open1").WithTitle("Open Issue")
            .WithStatus(IssueStatus.Open).Build();
        var reviewIssue = new IssueBuilder().WithId("review1").WithTitle("Review Issue")
            .WithStatus(IssueStatus.Review).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([openIssue, reviewIssue]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("review1");
        result[1].Id.Should().Be("open1");
    }

    [Test]
    public async Task GetNextIssuesAsync_SortsIssuesWithDescriptionsFirst()
    {
        var noDesc = new IssueBuilder().WithId("noDesc").WithTitle("AAA No Description")
            .WithStatus(IssueStatus.Open).Build();
        var withDesc = new IssueBuilder().WithId("withDesc").WithTitle("ZZZ With Description")
            .WithStatus(IssueStatus.Open).WithDescription("Has a description").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([noDesc, withDesc]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("withDesc");
        result[1].Id.Should().Be("noDesc");
    }

    #endregion

    #region BuildTaskGraphLayoutAsync Tests (ported from TaskGraphServiceTests)

    [Test]
    public async Task BuildTaskGraphLayoutAsync_WithNoIssues_ReturnsEmptyGraph()
    {
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        result.Nodes.Should().BeEmpty();
        result.TotalLanes.Should().Be(0);
    }

    [Test]
    public async Task BuildTaskGraphLayoutAsync_WithSingleLeafIssue_ReturnsOneNodeAtLaneZero()
    {
        var issue = new IssueBuilder().WithId("issue1").WithTitle("Do something")
            .WithStatus(IssueStatus.Open).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        result.Nodes.Should().ContainSingle();
        result.Nodes[0].Issue.Id.Should().Be("issue1");
        result.Nodes[0].Lane.Should().Be(0);
        result.Nodes[0].Row.Should().Be(0);
        result.Nodes[0].IsActionable.Should().BeTrue();
        result.TotalLanes.Should().Be(1);
    }

    [Test]
    public async Task BuildTaskGraphLayoutAsync_ExcludesTerminalIssues()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();
        var closed = new IssueBuilder().WithId("closed1").WithStatus(IssueStatus.Closed).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([open, complete, closed]);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("open1");
    }

    [Test]
    public async Task BuildTaskGraphLayoutAsync_SeriesParentWithTwoLeafChildren_CorrectLanes()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();

        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup["child1"].Lane.Should().Be(0);
        nodeLookup["child2"].Lane.Should().Be(0);
        nodeLookup["parent"].Lane.Should().Be(1);

        result.Nodes.Select(n => n.Issue.Id).Should().ContainInOrder("child1", "child2", "parent");

        nodeLookup["child1"].IsActionable.Should().BeTrue();
        nodeLookup["child2"].IsActionable.Should().BeFalse();
        nodeLookup["parent"].IsActionable.Should().BeFalse();
    }

    [Test]
    public async Task BuildTaskGraphLayoutAsync_ParallelParentWithTwoLeafChildren_CorrectLanes()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();

        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup["child1"].Lane.Should().Be(0);
        nodeLookup["child2"].Lane.Should().Be(0);
        nodeLookup["parent"].Lane.Should().Be(1);

        nodeLookup["child1"].IsActionable.Should().BeTrue();
        nodeLookup["child2"].IsActionable.Should().BeTrue();
    }

    [Test]
    public async Task BuildTaskGraphLayoutAsync_GoToWorkExample_CorrectLaneAssignments()
    {
        var goToWork = new IssueBuilder().WithId("go-to-work").WithTitle("Go to work")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var wakeUp = new IssueBuilder().WithId("wake-up").WithTitle("Wake up")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("go-to-work", "aaa").Build();
        var makeBreakfast = new IssueBuilder().WithId("make-breakfast").WithTitle("Make breakfast")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel)
            .WithParentIssueIdAndOrder("go-to-work", "bbb").Build();
        var makeCoffee = new IssueBuilder().WithId("make-coffee").WithTitle("Make coffee")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("make-breakfast", "aaa").Build();
        var makeToast = new IssueBuilder().WithId("make-toast").WithTitle("Make toast")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series)
            .WithParentIssueIdAndOrder("make-breakfast", "bbb").Build();
        var toastBread = new IssueBuilder().WithId("toast-bread").WithTitle("Toast bread")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("make-toast", "aaa").Build();
        var spreadButter = new IssueBuilder().WithId("spread-butter").WithTitle("Spread butter")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("make-toast", "bbb").Build();
        var getInCar = new IssueBuilder().WithId("get-in-car").WithTitle("Get in car")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("go-to-work", "ccc").Build();
        var driveToWork = new IssueBuilder().WithId("drive-to-work").WithTitle("Drive to work")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("go-to-work", "ddd").Build();

        var allIssues = new List<Issue>
            { goToWork, wakeUp, makeBreakfast, makeCoffee, makeToast, toastBread, spreadButter, getInCar, driveToWork };

        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns(allIssues);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        result.Nodes.Should().HaveCount(9);

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);

        nodeLookup["wake-up"].Lane.Should().Be(0);
        nodeLookup["make-coffee"].Lane.Should().Be(1);
        nodeLookup["toast-bread"].Lane.Should().Be(1);
        nodeLookup["spread-butter"].Lane.Should().Be(1);
        nodeLookup["make-toast"].Lane.Should().Be(2);
        nodeLookup["make-breakfast"].Lane.Should().Be(3);
        nodeLookup["get-in-car"].Lane.Should().Be(3);
        nodeLookup["drive-to-work"].Lane.Should().Be(3);
        nodeLookup["go-to-work"].Lane.Should().Be(4);

        result.TotalLanes.Should().Be(5);
    }

    [Test]
    public async Task BuildTaskGraphLayoutAsync_OpenChildWithCompleteParent_ParentIncludedInGraph()
    {
        var parent = new IssueBuilder().WithId("go-to-work").WithTitle("Go to work")
            .WithStatus(IssueStatus.Complete).WithExecutionMode(ExecutionMode.Series).Build();
        var child = new IssueBuilder().WithId("drive-to-work").WithTitle("Drive to work")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("go-to-work", "aaa").Build();

        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child]);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        result.Nodes.Should().HaveCount(2);
        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);

        nodeLookup["drive-to-work"].Lane.Should().Be(0);
        nodeLookup["go-to-work"].Lane.Should().Be(1);

        nodeLookup["drive-to-work"].IsActionable.Should().BeTrue();
        nodeLookup["go-to-work"].IsActionable.Should().BeFalse();
    }

    [Test]
    public async Task BuildTaskGraphLayoutAsync_MultiParentIssue_AppearsOnlyOnce()
    {
        var parentA = new IssueBuilder().WithId("parentA").WithTitle("Parent A")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var parentB = new IssueBuilder().WithId("parentB").WithTitle("Parent B")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var sharedChild = new IssueBuilder().WithId("shared").WithTitle("Shared Child")
            .WithStatus(IssueStatus.Open)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parentA", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "parentB", SortOrder = "aaa" })
            .Build();

        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parentA, parentB, sharedChild]);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        result.Nodes.Count(n => n.Issue.Id == "shared").Should().Be(1);
        result.Nodes.Should().HaveCount(3);
    }

    #endregion

    #region QueryGraphAsync Tests

    [Test]
    public async Task QueryGraphAsync_StatusFilter_ReturnsOnlyMatchingStatus()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var review = new IssueBuilder().WithId("review1").WithStatus(IssueStatus.Review).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([open, review]);

        var result = await _sut.QueryGraphAsync(new GraphQuery { Status = IssueStatus.Open });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("open1");
    }

    [Test]
    public async Task QueryGraphAsync_TypeFilter_ReturnsOnlyMatchingType()
    {
        var task = new IssueBuilder().WithId("task1").WithType(IssueType.Task).Build();
        var bug = new IssueBuilder().WithId("bug1").WithType(IssueType.Bug).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([task, bug]);

        var result = await _sut.QueryGraphAsync(new GraphQuery { Type = IssueType.Bug });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("bug1");
    }

    [Test]
    public async Task QueryGraphAsync_TagsFilter_ReturnsMatchingTags()
    {
        var tagged = new IssueBuilder().WithId("tagged1").WithTags("important", "urgent").Build();
        var untagged = new IssueBuilder().WithId("untagged1").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([tagged, untagged]);

        var result = await _sut.QueryGraphAsync(new GraphQuery { Tags = ["important"] });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("tagged1");
    }

    [Test]
    public async Task QueryGraphAsync_SearchText_MatchesTitle()
    {
        var matches = new IssueBuilder().WithId("match1").WithTitle("Fix the login bug").Build();
        var noMatch = new IssueBuilder().WithId("nomatch1").WithTitle("Add new feature").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([matches, noMatch]);

        var result = await _sut.QueryGraphAsync(new GraphQuery { SearchText = "login" });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("match1");
    }

    [Test]
    public async Task QueryGraphAsync_SearchText_MatchesDescription()
    {
        var matches = new IssueBuilder().WithId("match1").WithTitle("Issue").WithDescription("The login is broken").Build();
        var noMatch = new IssueBuilder().WithId("nomatch1").WithTitle("Issue2").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([matches, noMatch]);

        var result = await _sut.QueryGraphAsync(new GraphQuery { SearchText = "login" });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("match1");
    }

    [Test]
    public async Task QueryGraphAsync_RootIssueId_ReturnsDescendantsAndRoot()
    {
        var root = new IssueBuilder().WithId("root").WithStatus(IssueStatus.Open).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("root", "aaa").Build();
        var other = new IssueBuilder().WithId("other").WithStatus(IssueStatus.Open).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([root, child, other]);

        var result = await _sut.QueryGraphAsync(new GraphQuery { RootIssueId = "root" });

        result.Nodes.Should().HaveCount(2);
        result.Nodes.Should().ContainKey("root");
        result.Nodes.Should().ContainKey("child");
        result.Nodes.Should().NotContainKey("other");
    }

    [Test]
    public async Task QueryGraphAsync_IncludeTerminal_IncludesTerminalIssues()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([open, complete]);

        var result = await _sut.QueryGraphAsync(new GraphQuery { IncludeTerminal = true });

        result.Nodes.Should().HaveCount(2);
    }

    [Test]
    public async Task QueryGraphAsync_ExcludesTerminalByDefault()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([open, complete]);

        var result = await _sut.QueryGraphAsync(new GraphQuery());

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("open1");
    }

    [Test]
    public async Task QueryGraphAsync_IncludeInactiveWithActiveDescendants_IncludesTerminalParent()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Complete).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child]);

        var result = await _sut.QueryGraphAsync(new GraphQuery
        {
            IncludeInactiveWithActiveDescendants = true
        });

        result.Nodes.Should().HaveCount(2);
        result.Nodes.Should().ContainKey("parent");
        result.Nodes.Should().ContainKey("child");
    }

    [Test]
    public async Task QueryGraphAsync_AssignedToFilter_ReturnsMatchingAssignee()
    {
        var assigned = new IssueBuilder().WithId("assigned1").WithAssignedTo("john").Build();
        var other = new IssueBuilder().WithId("other1").WithAssignedTo("jane").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([assigned, other]);

        var result = await _sut.QueryGraphAsync(new GraphQuery { AssignedTo = "john" });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("assigned1");
    }

    [Test]
    public async Task QueryGraphAsync_PriorityFilter_ReturnsMatchingPriority()
    {
        var highPri = new IssueBuilder().WithId("high1").WithPriority(1).Build();
        var lowPri = new IssueBuilder().WithId("low1").WithPriority(5).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([highPri, lowPri]);

        var result = await _sut.QueryGraphAsync(new GraphQuery { Priority = 1 });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("high1");
    }

    [Test]
    public async Task QueryGraphAsync_LinkedPrFilter_ReturnsMatchingPR()
    {
        var withPr = new IssueBuilder().WithId("pr1").WithLinkedPr(123).Build();
        var noPr = new IssueBuilder().WithId("nopr1").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([withPr, noPr]);

        var result = await _sut.QueryGraphAsync(new GraphQuery { LinkedPr = 123 });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("pr1");
    }

    [Test]
    public async Task QueryGraphAsync_FilteredSubgraph_PreservesNextPreviousFromFullGraph()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child1, child2]);

        // Even when filtered, Next/Previous should reflect full graph
        var result = await _sut.QueryGraphAsync(new GraphQuery());

        result.Nodes["child1"].NextIssueIds.Should().Contain("child2");
        result.Nodes["child2"].PreviousIssueIds.Should().Contain("child1");
    }

    #endregion

    #region ParentExecutionMode Tests

    [Test]
    public async Task BuildGraphAsync_RootIssue_HasNullParentExecutionMode()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Open).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([issue]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes["issue1"].ParentExecutionMode.Should().BeNull();
    }

    [Test]
    public async Task BuildGraphAsync_ChildOfSeriesParent_HasSeriesParentExecutionMode()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes["child"].ParentExecutionMode.Should().Be(ExecutionMode.Series);
    }

    [Test]
    public async Task BuildGraphAsync_ChildOfParallelParent_HasParallelParentExecutionMode()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, child]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes["child"].ParentExecutionMode.Should().Be(ExecutionMode.Parallel);
    }

    #endregion

    #region Idea Type Exclusion Tests

    [Test]
    public async Task GetNextIssuesAsync_WithIdeaTypeIssue_ExcludesIdea()
    {
        var idea = new IssueBuilder().WithId("idea1").WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([idea]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetNextIssuesAsync_WithMixedTypeIssues_ExcludesOnlyIdeas()
    {
        var task = new IssueBuilder().WithId("task1").WithStatus(IssueStatus.Open).WithType(IssueType.Task).Build();
        var bug = new IssueBuilder().WithId("bug1").WithStatus(IssueStatus.Open).WithType(IssueType.Bug).Build();
        var idea = new IssueBuilder().WithId("idea1").WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();
        var feature = new IssueBuilder().WithId("feature1").WithStatus(IssueStatus.Open).WithType(IssueType.Feature).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([task, bug, idea, feature]);

        var result = await _sut.GetNextIssuesAsync();

        result.Select(i => i.Id).Should().BeEquivalentTo(["task1", "bug1", "feature1"]);
        result.Select(i => i.Id).Should().NotContain("idea1");
    }

    [Test]
    public async Task GetNextIssuesAsync_WithIdeaInReviewStatus_StillExcludesIdea()
    {
        var idea = new IssueBuilder().WithId("idea1").WithStatus(IssueStatus.Review).WithType(IssueType.Idea).Build();
        var task = new IssueBuilder().WithId("task1").WithStatus(IssueStatus.Review).WithType(IssueType.Task).Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([idea, task]);

        var result = await _sut.GetNextIssuesAsync();

        result.Should().ContainSingle().Which.Id.Should().Be("task1");
    }

    [Test]
    public async Task GetNextIssuesAsync_ParentWithIdeaChild_IdeaChildNotActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var taskChild = new IssueBuilder().WithId("task-child").WithStatus(IssueStatus.Open).WithType(IssueType.Task).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var ideaChild = new IssueBuilder().WithId("idea-child").WithStatus(IssueStatus.Open).WithType(IssueType.Idea).WithParentIssueIdAndOrder("parent", "bbb").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, taskChild, ideaChild]);

        var result = await _sut.GetNextIssuesAsync();

        result.Select(i => i.Id).Should().BeEquivalentTo(["task-child"]);
    }

    #endregion

    #region Idea Type Task Graph Root Exclusion Tests

    [Test]
    public async Task BuildTaskGraphLayoutAsync_IdeaTypeRootIssue_ExcludedFromRoots()
    {
        var idea = new IssueBuilder().WithId("idea1").WithTitle("Idea Issue")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();
        var task = new IssueBuilder().WithId("task1").WithTitle("Task Issue")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Task).Build();

        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([idea, task]);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("task1");
    }

    [Test]
    public async Task BuildTaskGraphLayoutAsync_IdeaAsChildOfTask_IdeaIncludedInGraph()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent Task")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Task).WithExecutionMode(ExecutionMode.Parallel).Build();
        var ideaChild = new IssueBuilder().WithId("idea-child").WithTitle("Idea Child")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var taskChild = new IssueBuilder().WithId("task-child").WithTitle("Task Child")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Task).WithParentIssueIdAndOrder("parent", "bbb").Build();

        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([parent, ideaChild, taskChild]);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        result.Nodes.Should().HaveCount(3);
        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup.Should().ContainKey("parent");
        nodeLookup.Should().ContainKey("idea-child");
        nodeLookup.Should().ContainKey("task-child");

        nodeLookup["idea-child"].IsActionable.Should().BeFalse();
        nodeLookup["task-child"].IsActionable.Should().BeTrue();
    }

    [Test]
    public async Task BuildTaskGraphLayoutAsync_MultipleIdeaRoots_AllExcluded()
    {
        var idea1 = new IssueBuilder().WithId("idea1").WithTitle("Idea 1")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();
        var idea2 = new IssueBuilder().WithId("idea2").WithTitle("Idea 2")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();
        var task = new IssueBuilder().WithId("task1").WithTitle("Task 1")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Task).Build();

        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([idea1, idea2, task]);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("task1");
    }

    [Test]
    public async Task BuildTaskGraphLayoutAsync_IdeaWithOrphanedParent_TreatedAsRootAndExcluded()
    {
        var idea = new IssueBuilder().WithId("idea1").WithTitle("Orphan Idea")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).WithParentIssueIdAndOrder("nonexistent", "aaa").Build();
        var task = new IssueBuilder().WithId("task1").WithTitle("Task 1")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Task).Build();

        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([idea, task]);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("task1");
    }

    [Test]
    public async Task BuildTaskGraphLayoutAsync_OnlyIdeasInGraph_ReturnsEmptyGraph()
    {
        var idea1 = new IssueBuilder().WithId("idea1").WithTitle("Idea 1")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();
        var idea2 = new IssueBuilder().WithId("idea2").WithTitle("Idea 2")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();

        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([idea1, idea2]);

        var result = await _sut.BuildTaskGraphLayoutAsync();

        result.Nodes.Should().BeEmpty();
        result.TotalLanes.Should().Be(0);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task BuildGraphAsync_OrphanIssue_TreatedAsRoot()
    {
        var orphan = new IssueBuilder().WithId("orphan").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("nonexistent", "aaa").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>()).Returns([orphan]);

        var result = await _sut.BuildGraphAsync();

        result.Nodes.Should().ContainSingle();
        result.RootIssueIds.Should().ContainSingle().Which.Should().Be("orphan");
        // Parent doesn't exist in graph, so ParentIssueIds should be empty
        result.Nodes["orphan"].ParentIssueIds.Should().BeEmpty();
    }

    [Test]
    public async Task GetNextIssuesAsync_NestedHierarchy_RespectsParentExecutionModes()
    {
        var grandparent = new IssueBuilder().WithId("grandparent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var parent1 = new IssueBuilder().WithId("parent1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("grandparent", "aaa").WithExecutionMode(ExecutionMode.Series).Build();
        var parent2 = new IssueBuilder().WithId("parent2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("grandparent", "bbb").WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1a = new IssueBuilder().WithId("child1a").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var child1b = new IssueBuilder().WithId("child1b").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent1", "bbb").Build();
        var child2a = new IssueBuilder().WithId("child2a").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent2", "aaa").Build();
        var child2b = new IssueBuilder().WithId("child2b").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent2", "bbb").Build();
        _storageService.LoadIssuesAsync(Arg.Any<CancellationToken>())
            .Returns([grandparent, parent1, parent2, child1a, child1b, child2a, child2b]);

        var result = await _sut.GetNextIssuesAsync();

        // parent1 is series, so only child1a is actionable (not child1b)
        // parent2 is parallel, so both child2a and child2b are actionable
        result.Select(i => i.Id).Should().BeEquivalentTo(["child1a", "child2a", "child2b"]);
    }

    #endregion
}
