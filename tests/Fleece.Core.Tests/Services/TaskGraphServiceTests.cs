using Fleece.Core.Models;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class TaskGraphServiceTests
{
    private IIssueService _issueService = null!;
    private INextService _nextService = null!;
    private TaskGraphService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _issueService = Substitute.For<IIssueService>();
        _nextService = Substitute.For<INextService>();
        _sut = new TaskGraphService(_issueService, _nextService);
    }

    #region Empty / Basic Tests

    [Test]
    public async Task BuildGraphAsync_WithNoIssues_ReturnsEmptyGraph()
    {
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue>());

        var result = await _sut.BuildGraphAsync();

        result.Nodes.Should().BeEmpty();
        result.TotalLanes.Should().Be(0);
    }

    [Test]
    public async Task BuildGraphAsync_WithSingleLeafIssue_ReturnsOneNodeAtLaneZero()
    {
        var issue = new IssueBuilder().WithId("issue1").WithTitle("Do something").WithStatus(IssueStatus.Open).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { issue });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { issue });

        var result = await _sut.BuildGraphAsync();

        result.Nodes.Should().ContainSingle();
        result.Nodes[0].Issue.Id.Should().Be("issue1");
        result.Nodes[0].Lane.Should().Be(0);
        result.Nodes[0].Row.Should().Be(0);
        result.Nodes[0].IsActionable.Should().BeTrue();
        result.TotalLanes.Should().Be(1);
    }

    [Test]
    public async Task BuildGraphAsync_ExcludesTerminalIssues()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();
        var closed = new IssueBuilder().WithId("closed1").WithStatus(IssueStatus.Closed).Build();
        var deleted = new IssueBuilder().WithId("deleted1").WithStatus(IssueStatus.Deleted).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { open, complete, closed, deleted });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { open });

        var result = await _sut.BuildGraphAsync();

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("open1");
    }

    #endregion

    #region Series Execution Mode Tests

    [Test]
    public async Task BuildGraphAsync_SeriesParentWithTwoLeafChildren_CorrectLanes()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { parent, child1, child2 });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { child1 });

        var result = await _sut.BuildGraphAsync();

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        // Both serial children at lane 0, parent at lane 1
        nodeLookup["child1"].Lane.Should().Be(0);
        nodeLookup["child2"].Lane.Should().Be(0);
        nodeLookup["parent"].Lane.Should().Be(1);

        // child1 should be first (top), child2 second, parent last
        result.Nodes.Select(n => n.Issue.Id).Should().ContainInOrder("child1", "child2", "parent");

        // Only child1 is actionable
        nodeLookup["child1"].IsActionable.Should().BeTrue();
        nodeLookup["child2"].IsActionable.Should().BeFalse();
        nodeLookup["parent"].IsActionable.Should().BeFalse();
    }

    [Test]
    public async Task BuildGraphAsync_SeriesParentWithThreeLeafChildren_AllAtSameLane()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        var child3 = new IssueBuilder().WithId("child3").WithTitle("Child 3").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "ccc").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { parent, child1, child2, child3 });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { child1 });

        var result = await _sut.BuildGraphAsync();

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup["child1"].Lane.Should().Be(0);
        nodeLookup["child2"].Lane.Should().Be(0);
        nodeLookup["child3"].Lane.Should().Be(0);
        nodeLookup["parent"].Lane.Should().Be(1);

        result.Nodes.Select(n => n.Issue.Id).Should().ContainInOrder("child1", "child2", "child3", "parent");
    }

    [Test]
    public async Task BuildGraphAsync_SeriesWithCompletedFirstChild_SecondChildStillAtLaneZero()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1").WithStatus(IssueStatus.Complete)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { parent, child1, child2 });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { child2 });

        var result = await _sut.BuildGraphAsync();

        // Completed child1 is excluded; child2 is now actionable at lane 0
        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup.Should().NotContainKey("child1");
        nodeLookup["child2"].Lane.Should().Be(0);
        nodeLookup["child2"].IsActionable.Should().BeTrue();
        nodeLookup["parent"].Lane.Should().Be(1);
    }

    #endregion

    #region Parallel Execution Mode Tests

    [Test]
    public async Task BuildGraphAsync_ParallelParentWithTwoLeafChildren_CorrectLanes()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { parent, child1, child2 });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { child1, child2 });

        var result = await _sut.BuildGraphAsync();

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        // Both parallel children at same lane, parent one lane higher
        nodeLookup["child1"].Lane.Should().Be(0);
        nodeLookup["child2"].Lane.Should().Be(0);
        nodeLookup["parent"].Lane.Should().Be(1);

        // Both are actionable
        nodeLookup["child1"].IsActionable.Should().BeTrue();
        nodeLookup["child2"].IsActionable.Should().BeTrue();
    }

    [Test]
    public async Task BuildGraphAsync_ParallelWithSubtreeChild_SubtreePushesParentRight()
    {
        // Parent (parallel)
        //   - LeafChild
        //   - SubtreeParent (series)
        //     - SubChild1
        //     - SubChild2
        var root = new IssueBuilder().WithId("root").WithTitle("Root").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var leaf = new IssueBuilder().WithId("leaf").WithTitle("Leaf").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("root", "aaa").Build();
        var subtreeParent = new IssueBuilder().WithId("sub-parent").WithTitle("Sub Parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series)
            .WithParentIssueIdAndOrder("root", "bbb").Build();
        var subChild1 = new IssueBuilder().WithId("sub-child1").WithTitle("Sub Child 1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("sub-parent", "aaa").Build();
        var subChild2 = new IssueBuilder().WithId("sub-child2").WithTitle("Sub Child 2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("sub-parent", "bbb").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { root, leaf, subtreeParent, subChild1, subChild2 });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { leaf, subChild1 });

        var result = await _sut.BuildGraphAsync();

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        // Leaf at lane 0
        nodeLookup["leaf"].Lane.Should().Be(0);
        // SubChild1 and SubChild2 at lane 0 (parallel children share startLane)
        nodeLookup["sub-child1"].Lane.Should().Be(0);
        nodeLookup["sub-child2"].Lane.Should().Be(0);
        // SubtreeParent at lane 1
        nodeLookup["sub-parent"].Lane.Should().Be(1);
        // Root at max(0, 1) + 1 = lane 2
        nodeLookup["root"].Lane.Should().Be(2);
    }

    #endregion

    #region Nested Hierarchy Tests (Go to Work Example)

    [Test]
    public async Task BuildGraphAsync_GoToWorkExample_CorrectLaneAssignments()
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

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(allIssues);
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { wakeUp });

        var result = await _sut.BuildGraphAsync();

        // Verify all 9 nodes present
        result.Nodes.Should().HaveCount(9);

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);

        // Verify lane assignments
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
    public async Task BuildGraphAsync_GoToWorkExample_CorrectRowOrdering()
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

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(allIssues);
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { wakeUp });

        var result = await _sut.BuildGraphAsync();

        // Verify exact row ordering matches the example
        var order = result.Nodes.Select(n => n.Issue.Id).ToList();
        order.Should().Equal(
            "wake-up",
            "make-coffee",
            "toast-bread",
            "spread-butter",
            "make-toast",
            "make-breakfast",
            "get-in-car",
            "drive-to-work",
            "go-to-work"
        );
    }

    [Test]
    public async Task BuildGraphAsync_GoToWorkExample_CorrectActionability()
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

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(allIssues);
        // Only Wake up is actionable (Go to work is series, Wake up is first incomplete child)
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { wakeUp });

        var result = await _sut.BuildGraphAsync();

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup["wake-up"].IsActionable.Should().BeTrue();
        nodeLookup["make-coffee"].IsActionable.Should().BeFalse();
        nodeLookup["toast-bread"].IsActionable.Should().BeFalse();
        nodeLookup["spread-butter"].IsActionable.Should().BeFalse();
        nodeLookup["make-toast"].IsActionable.Should().BeFalse();
        nodeLookup["make-breakfast"].IsActionable.Should().BeFalse();
        nodeLookup["get-in-car"].IsActionable.Should().BeFalse();
        nodeLookup["drive-to-work"].IsActionable.Should().BeFalse();
        nodeLookup["go-to-work"].IsActionable.Should().BeFalse();
    }

    #endregion

    #region Step Progression Tests

    [Test]
    public async Task BuildGraphAsync_GoToWorkStep1_WakeUpComplete_CoffeeAndToastActionable()
    {
        // After Wake up is completed: Make coffee and Toast bread become actionable
        var goToWork = new IssueBuilder().WithId("go-to-work").WithTitle("Go to work")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var wakeUp = new IssueBuilder().WithId("wake-up").WithTitle("Wake up")
            .WithStatus(IssueStatus.Complete).WithParentIssueIdAndOrder("go-to-work", "aaa").Build();
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

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(allIssues);
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { makeCoffee, toastBread });

        var result = await _sut.BuildGraphAsync();

        // Wake up is excluded (complete/terminal)
        result.Nodes.Should().HaveCount(8);
        result.Nodes.Select(n => n.Issue.Id).Should().NotContain("wake-up");

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup["make-coffee"].IsActionable.Should().BeTrue();
        nodeLookup["toast-bread"].IsActionable.Should().BeTrue();
        nodeLookup["spread-butter"].IsActionable.Should().BeFalse();
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task BuildGraphAsync_AllChildrenDone_ParentTreatedAsLeaf()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1").WithStatus(IssueStatus.Complete)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2").WithStatus(IssueStatus.Closed)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { parent, child1, child2 });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { parent });

        var result = await _sut.BuildGraphAsync();

        // Only parent should be in the graph (children are terminal)
        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("parent");
        result.Nodes[0].Lane.Should().Be(0);
        result.Nodes[0].IsActionable.Should().BeTrue();
    }

    [Test]
    public async Task BuildGraphAsync_MultipleRoots_EachGetsOwnSubtree()
    {
        var root1 = new IssueBuilder().WithId("root1").WithTitle("Root 1").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var root1Child = new IssueBuilder().WithId("r1-child").WithTitle("R1 Child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("root1", "aaa").Build();

        var root2 = new IssueBuilder().WithId("root2").WithTitle("Root 2").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var root2Child = new IssueBuilder().WithId("r2-child").WithTitle("R2 Child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("root2", "aaa").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { root1, root1Child, root2, root2Child });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { root1Child, root2Child });

        var result = await _sut.BuildGraphAsync();

        result.Nodes.Should().HaveCount(4);
        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup["r1-child"].Lane.Should().Be(0);
        nodeLookup["root1"].Lane.Should().Be(1);
        nodeLookup["r2-child"].Lane.Should().Be(0);
        nodeLookup["root2"].Lane.Should().Be(1);
    }

    [Test]
    public async Task BuildGraphAsync_OrphanIssue_TreatedAsRoot()
    {
        var orphan = new IssueBuilder().WithId("orphan").WithTitle("Orphan").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("nonexistent", "aaa").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { orphan });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { orphan });

        var result = await _sut.BuildGraphAsync();

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("orphan");
        result.Nodes[0].Lane.Should().Be(0);
    }

    [Test]
    public async Task BuildGraphAsync_SeriesWithSubtreeFollowedByLeaf_LeafInheritsSubtreeLane()
    {
        // Root (series)
        //   - SubtreeParent (series)
        //     - SubChild1
        //     - SubChild2
        //   - LeafChild
        var root = new IssueBuilder().WithId("root").WithTitle("Root").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var subtreeParent = new IssueBuilder().WithId("sub-parent").WithTitle("Sub Parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series)
            .WithParentIssueIdAndOrder("root", "aaa").Build();
        var subChild1 = new IssueBuilder().WithId("sub-child1").WithTitle("Sub Child 1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("sub-parent", "aaa").Build();
        var subChild2 = new IssueBuilder().WithId("sub-child2").WithTitle("Sub Child 2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("sub-parent", "bbb").Build();
        var leafChild = new IssueBuilder().WithId("leaf").WithTitle("Leaf").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("root", "bbb").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { root, subtreeParent, subChild1, subChild2, leafChild });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { subChild1 });

        var result = await _sut.BuildGraphAsync();

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        // First child (subtree): subChild1 and subChild2 at lane 0, sub-parent at lane 1
        nodeLookup["sub-child1"].Lane.Should().Be(0);
        nodeLookup["sub-child2"].Lane.Should().Be(0);
        nodeLookup["sub-parent"].Lane.Should().Be(1);
        // Leaf after subtree inherits the subtree's resolved lane (1)
        nodeLookup["leaf"].Lane.Should().Be(1);
        // Root at max(1) + 1 = 2
        nodeLookup["root"].Lane.Should().Be(2);

        // Verify ordering
        result.Nodes.Select(n => n.Issue.Id).Should().ContainInOrder(
            "sub-child1", "sub-child2", "sub-parent", "leaf", "root");
    }

    [Test]
    public async Task BuildGraphAsync_SeriesWithLeafThenSubtree_SubtreeStartsOneLanePastLeaf()
    {
        // Root (series)
        //   - LeafChild
        //   - SubtreeParent (series)
        //     - SubChild1
        //     - SubChild2
        var root = new IssueBuilder().WithId("root").WithTitle("Root").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var leafChild = new IssueBuilder().WithId("leaf").WithTitle("Leaf").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("root", "aaa").Build();
        var subtreeParent = new IssueBuilder().WithId("sub-parent").WithTitle("Sub Parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series)
            .WithParentIssueIdAndOrder("root", "bbb").Build();
        var subChild1 = new IssueBuilder().WithId("sub-child1").WithTitle("Sub Child 1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("sub-parent", "aaa").Build();
        var subChild2 = new IssueBuilder().WithId("sub-child2").WithTitle("Sub Child 2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("sub-parent", "bbb").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { root, leafChild, subtreeParent, subChild1, subChild2 });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { leafChild });

        var result = await _sut.BuildGraphAsync();

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        // Leaf at lane 0 (first series child)
        nodeLookup["leaf"].Lane.Should().Be(0);
        // Subtree starts at currentLane + 1 = 1 (non-first child with subtree)
        nodeLookup["sub-child1"].Lane.Should().Be(1);
        nodeLookup["sub-child2"].Lane.Should().Be(1);
        nodeLookup["sub-parent"].Lane.Should().Be(2);
        // Root at max(2) + 1 = 3
        nodeLookup["root"].Lane.Should().Be(3);

        // Verify ordering
        result.Nodes.Select(n => n.Issue.Id).Should().ContainInOrder(
            "leaf", "sub-child1", "sub-child2", "sub-parent", "root");
    }

    [Test]
    public async Task BuildGraphAsync_MultiParentIssue_AppearsOnlyOnce()
    {
        // Shared child has two parents (DAG). It should appear exactly once
        // under whichever parent is traversed first, and not cause a duplicate key crash.
        //
        // parentA (series)   parentB (series)
        //        \              /
        //         shared-child
        var parentA = new IssueBuilder().WithId("parentA").WithTitle("Parent A").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var parentB = new IssueBuilder().WithId("parentB").WithTitle("Parent B").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var sharedChild = new IssueBuilder().WithId("shared").WithTitle("Shared Child").WithStatus(IssueStatus.Open)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parentA", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "parentB", SortOrder = "aaa" })
            .Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { parentA, parentB, sharedChild });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { sharedChild });

        var result = await _sut.BuildGraphAsync();

        // Shared child should appear exactly once (no duplicate key exception)
        result.Nodes.Count(n => n.Issue.Id == "shared").Should().Be(1);

        // All three issues should be present
        result.Nodes.Should().HaveCount(3);

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup.Should().ContainKey("shared");
        nodeLookup.Should().ContainKey("parentA");
        nodeLookup.Should().ContainKey("parentB");
    }

    [Test]
    public async Task BuildGraphAsync_InProgressIssue_IncludedButNotActionable()
    {
        var issue = new IssueBuilder().WithId("issue1").WithTitle("In Progress").WithStatus(IssueStatus.Progress).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { issue });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue>());

        var result = await _sut.BuildGraphAsync();

        result.Nodes.Should().ContainSingle();
        result.Nodes[0].IsActionable.Should().BeFalse();
    }

    [Test]
    public async Task BuildGraphAsync_RowIndicesAreSequential()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { parent, child1, child2 });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { child1 });

        var result = await _sut.BuildGraphAsync();

        result.Nodes[0].Row.Should().Be(0);
        result.Nodes[1].Row.Should().Be(1);
        result.Nodes[2].Row.Should().Be(2);
    }

    #endregion

    #region ParentExecutionMode Tests

    [Test]
    public async Task BuildGraphAsync_RootIssue_HasNullParentExecutionMode()
    {
        var issue = new IssueBuilder().WithId("issue1").WithTitle("Root Issue").WithStatus(IssueStatus.Open).Build();
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { issue });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { issue });

        var result = await _sut.BuildGraphAsync();

        result.Nodes.Should().ContainSingle();
        result.Nodes[0].ParentExecutionMode.Should().BeNull();
    }

    [Test]
    public async Task BuildGraphAsync_SeriesParentChildren_HaveSeriesParentExecutionMode()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { parent, child1, child2 });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { child1 });

        var result = await _sut.BuildGraphAsync();

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup["child1"].ParentExecutionMode.Should().Be(ExecutionMode.Series);
        nodeLookup["child2"].ParentExecutionMode.Should().Be(ExecutionMode.Series);
        nodeLookup["parent"].ParentExecutionMode.Should().BeNull(); // root
    }

    [Test]
    public async Task BuildGraphAsync_ParallelParentChildren_HaveParallelParentExecutionMode()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { parent, child1, child2 });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { child1, child2 });

        var result = await _sut.BuildGraphAsync();

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup["child1"].ParentExecutionMode.Should().Be(ExecutionMode.Parallel);
        nodeLookup["child2"].ParentExecutionMode.Should().Be(ExecutionMode.Parallel);
        nodeLookup["parent"].ParentExecutionMode.Should().BeNull(); // root
    }

    [Test]
    public async Task BuildGraphAsync_NestedHierarchy_CorrectParentExecutionModes()
    {
        // Root (series)
        //   - SubtreeParent (parallel)
        //     - SubChild1 (leaf)
        //     - SubChild2 (leaf)
        //   - LeafChild
        var root = new IssueBuilder().WithId("root").WithTitle("Root").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var subtreeParent = new IssueBuilder().WithId("sub-parent").WithTitle("Sub Parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel)
            .WithParentIssueIdAndOrder("root", "aaa").Build();
        var subChild1 = new IssueBuilder().WithId("sub-child1").WithTitle("Sub Child 1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("sub-parent", "aaa").Build();
        var subChild2 = new IssueBuilder().WithId("sub-child2").WithTitle("Sub Child 2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("sub-parent", "bbb").Build();
        var leafChild = new IssueBuilder().WithId("leaf").WithTitle("Leaf").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("root", "bbb").Build();

        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue> { root, subtreeParent, subChild1, subChild2, leafChild });
        _nextService.GetNextIssuesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Issue> { subChild1, subChild2 });

        var result = await _sut.BuildGraphAsync();

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        // Grandchildren under parallel parent
        nodeLookup["sub-child1"].ParentExecutionMode.Should().Be(ExecutionMode.Parallel);
        nodeLookup["sub-child2"].ParentExecutionMode.Should().Be(ExecutionMode.Parallel);
        // SubtreeParent is child of series root
        nodeLookup["sub-parent"].ParentExecutionMode.Should().Be(ExecutionMode.Series);
        // LeafChild is child of series root
        nodeLookup["leaf"].ParentExecutionMode.Should().Be(ExecutionMode.Series);
        // Root has no parent
        nodeLookup["root"].ParentExecutionMode.Should().BeNull();
    }

    #endregion
}
