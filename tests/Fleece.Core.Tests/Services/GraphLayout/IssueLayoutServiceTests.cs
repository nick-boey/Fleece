using Fleece.Core.Models;
using Fleece.Core.Models.Graph;
using Fleece.Core.Services.GraphLayout;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services.GraphLayout;

[TestFixture]
public class IssueLayoutServiceTests
{
    private static IssueLayoutService NewService() => new(new GraphLayoutService());

    [Test]
    public void LayoutForTree_NoIssues_ReturnsEmpty()
    {
        var svc = NewService();
        var result = svc.LayoutForTree(Array.Empty<Issue>());
        result.Nodes.Should().BeEmpty();
        result.TotalLanes.Should().Be(0);
    }

    [Test]
    public void LayoutForTree_SingleLeafIssue_OneNodeAtLaneZero()
    {
        var issue = new IssueBuilder().WithId("issue1").WithTitle("Do something")
            .WithStatus(IssueStatus.Open).Build();
        var result = NewService().LayoutForTree(new[] { issue });
        result.Nodes.Should().ContainSingle();
        result.Nodes[0].Node.Id.Should().Be("issue1");
        result.Nodes[0].Lane.Should().Be(0);
        result.Nodes[0].Row.Should().Be(0);
        result.TotalLanes.Should().Be(1);
    }

    [Test]
    public void LayoutForTree_ExcludesTerminalIssuesByDefault()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();
        var closed = new IssueBuilder().WithId("closed1").WithStatus(IssueStatus.Closed).Build();

        var result = NewService().LayoutForTree(new[] { open, complete, closed });
        result.Nodes.Should().ContainSingle().Which.Node.Id.Should().Be("open1");
    }

    [Test]
    public void LayoutForTree_SeriesParentWithTwoLeafChildren_LanesMatchLegacy()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var c1 = new IssueBuilder().WithId("child1").WithTitle("Child 1")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var c2 = new IssueBuilder().WithId("child2").WithTitle("Child 2")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = NewService().LayoutForTree(new[] { parent, c1, c2 });

        var byId = result.Nodes.ToDictionary(n => n.Node.Id);
        byId["child1"].Lane.Should().Be(0);
        byId["child2"].Lane.Should().Be(0);
        byId["parent"].Lane.Should().Be(1);
        result.Nodes.Select(n => n.Node.Id).Should().ContainInOrder("child1", "child2", "parent");
    }

    [Test]
    public void LayoutForTree_ParallelParentWithTwoLeafChildren_LanesMatchLegacy()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var c1 = new IssueBuilder().WithId("child1").WithTitle("Child 1")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var c2 = new IssueBuilder().WithId("child2").WithTitle("Child 2")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = NewService().LayoutForTree(new[] { parent, c1, c2 });

        var byId = result.Nodes.ToDictionary(n => n.Node.Id);
        byId["child1"].Lane.Should().Be(0);
        byId["child2"].Lane.Should().Be(0);
        byId["parent"].Lane.Should().Be(1);
    }

    [Test]
    public void LayoutForTree_GoToWorkExample_LanesMatchLegacy()
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

        var result = NewService().LayoutForTree(allIssues);
        result.Nodes.Should().HaveCount(9);

        var byId = result.Nodes.ToDictionary(n => n.Node.Id);
        byId["wake-up"].Lane.Should().Be(0);
        byId["make-coffee"].Lane.Should().Be(1);
        byId["toast-bread"].Lane.Should().Be(1);
        byId["spread-butter"].Lane.Should().Be(1);
        byId["make-toast"].Lane.Should().Be(2);
        byId["make-breakfast"].Lane.Should().Be(3);
        byId["get-in-car"].Lane.Should().Be(3);
        byId["drive-to-work"].Lane.Should().Be(3);
        byId["go-to-work"].Lane.Should().Be(4);
        result.TotalLanes.Should().Be(5);
    }

    [Test]
    public void LayoutForTree_OpenChildWithCompleteParent_ParentIncludedInGraph()
    {
        var parent = new IssueBuilder().WithId("go-to-work").WithTitle("Go to work")
            .WithStatus(IssueStatus.Complete).WithExecutionMode(ExecutionMode.Series).Build();
        var child = new IssueBuilder().WithId("drive-to-work").WithTitle("Drive to work")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("go-to-work", "aaa").Build();

        var result = NewService().LayoutForTree(new[] { parent, child });

        result.Nodes.Should().HaveCount(2);
        var byId = result.Nodes.ToDictionary(n => n.Node.Id);
        byId["drive-to-work"].Lane.Should().Be(0);
        byId["go-to-work"].Lane.Should().Be(1);
    }

    [Test]
    public void LayoutForTree_MultiParentIssue_AppearsUnderEachParent()
    {
        var pa = new IssueBuilder().WithId("parentA").WithTitle("Parent A")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var pb = new IssueBuilder().WithId("parentB").WithTitle("Parent B")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var shared = new IssueBuilder().WithId("shared").WithTitle("Shared Child")
            .WithStatus(IssueStatus.Open)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parentA", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "parentB", SortOrder = "aaa" })
            .Build();

        var result = NewService().LayoutForTree(new[] { pa, pb, shared });

        var sharedNodes = result.Nodes.Where(n => n.Node.Id == "shared").ToList();
        sharedNodes.Should().HaveCount(2);
        sharedNodes[0].AppearanceIndex.Should().Be(1);
        sharedNodes[0].TotalAppearances.Should().Be(2);
        sharedNodes[1].AppearanceIndex.Should().Be(2);
        sharedNodes[1].TotalAppearances.Should().Be(2);
        sharedNodes[0].Node.Should().BeSameAs(sharedNodes[1].Node);
    }

    [Test]
    public void LayoutForTree_MultiParentWithChildren_ChildrenOnlyUnderFirstParent()
    {
        var pa = new IssueBuilder().WithId("parentA").WithTitle("Parent A")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var pb = new IssueBuilder().WithId("parentB").WithTitle("Parent B")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var shared = new IssueBuilder().WithId("shared").WithTitle("Shared Child")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parentA", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "parentB", SortOrder = "aaa" })
            .Build();
        var grand = new IssueBuilder().WithId("grand").WithTitle("Grandchild")
            .WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("shared", "aaa").Build();

        var result = NewService().LayoutForTree(new[] { pa, pb, shared, grand });

        result.Nodes.Count(n => n.Node.Id == "shared").Should().Be(2);
        result.Nodes.Count(n => n.Node.Id == "grand").Should().Be(1);
    }

    [Test]
    public void LayoutForTree_DiamondDependency_SharedIssueAppearsTwice()
    {
        var a = new IssueBuilder().WithId("A").WithTitle("A")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var b = new IssueBuilder().WithId("B").WithTitle("B")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series)
            .WithParentIssueIdAndOrder("A", "aaa").Build();
        var c = new IssueBuilder().WithId("C").WithTitle("C")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series)
            .WithParentIssueIdAndOrder("A", "bbb").Build();
        var d = new IssueBuilder().WithId("D").WithTitle("D")
            .WithStatus(IssueStatus.Open)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "B", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "C", SortOrder = "aaa" })
            .Build();

        var result = NewService().LayoutForTree(new[] { a, b, c, d });

        var dNodes = result.Nodes.Where(n => n.Node.Id == "D").ToList();
        dNodes.Should().HaveCount(2);
        dNodes[0].TotalAppearances.Should().Be(2);
    }

    [Test]
    public void LayoutForTree_SingleParent_DefaultAppearanceCounts()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var child = new IssueBuilder().WithId("child").WithTitle("Child")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = NewService().LayoutForTree(new[] { parent, child });
        foreach (var n in result.Nodes)
        {
            n.AppearanceIndex.Should().Be(1);
            n.TotalAppearances.Should().Be(1);
        }
    }

    [Test]
    public void LayoutForTree_IdeaTypeRootIssue_IncludedInRoots()
    {
        var idea = new IssueBuilder().WithId("idea1").WithTitle("Idea Issue")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();
        var task = new IssueBuilder().WithId("task1").WithTitle("Task Issue")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Task).Build();

        var result = NewService().LayoutForTree(new[] { idea, task });
        result.Nodes.Should().HaveCount(2);
        result.Nodes.Select(n => n.Node.Id).Should().Contain(new[] { "idea1", "task1" });
    }

    [Test]
    public void LayoutForTree_IncludesDraftByDefault()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var draft = new IssueBuilder().WithId("draft1").WithStatus(IssueStatus.Draft).Build();
        var result = NewService().LayoutForTree(new[] { open, draft });
        result.Nodes.Should().HaveCount(2);
    }

    [Test]
    public void LayoutForTree_IncludeTerminal_IncludesTerminalStatuses()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var draft = new IssueBuilder().WithId("draft1").WithStatus(IssueStatus.Draft).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();
        var closed = new IssueBuilder().WithId("closed1").WithStatus(IssueStatus.Closed).Build();

        var result = NewService().LayoutForTree(
            new[] { open, draft, complete, closed }, visibility: InactiveVisibility.Always);
        result.Nodes.Should().HaveCount(4);
    }

    [Test]
    public void LayoutForTree_AssignedToFilter_ReturnsOnlyMatchingAssignee()
    {
        var john = new IssueBuilder().WithId("john1").WithStatus(IssueStatus.Open).WithAssignedTo("john").Build();
        var jane = new IssueBuilder().WithId("jane1").WithStatus(IssueStatus.Open).WithAssignedTo("jane").Build();
        var unassigned = new IssueBuilder().WithId("unassigned1").WithStatus(IssueStatus.Open).Build();

        var result = NewService().LayoutForTree(new[] { john, jane, unassigned }, assignedTo: "john");
        result.Nodes.Should().ContainSingle().Which.Node.Id.Should().Be("john1");
    }

    [Test]
    public void LayoutForTree_AssignedToFilter_IsCaseInsensitive()
    {
        var john = new IssueBuilder().WithId("john1").WithStatus(IssueStatus.Open).WithAssignedTo("John").Build();
        var result = NewService().LayoutForTree(new[] { john }, assignedTo: "john");
        result.Nodes.Should().ContainSingle().Which.Node.Id.Should().Be("john1");
    }

    [Test]
    public void LayoutForTree_IfHasActiveDescendants_IncludesTerminalParentWithActiveChild()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Complete).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = NewService().LayoutForTree(
            new[] { parent, child }, visibility: InactiveVisibility.IfHasActiveDescendants);
        result.Nodes.Should().HaveCount(2);
    }

    [Test]
    public void LayoutForTree_IfHasActiveDescendants_ExcludesTerminalWithNoActiveDescendants()
    {
        var terminalParent = new IssueBuilder().WithId("terminal-parent").WithStatus(IssueStatus.Complete).Build();
        var terminalChild = new IssueBuilder().WithId("terminal-child").WithStatus(IssueStatus.Closed)
            .WithParentIssueIdAndOrder("terminal-parent", "aaa").Build();
        var openAlone = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();

        var result = NewService().LayoutForTree(
            new[] { terminalParent, terminalChild, openAlone }, visibility: InactiveVisibility.IfHasActiveDescendants);
        result.Nodes.Should().ContainSingle().Which.Node.Id.Should().Be("open1");
    }

    [Test]
    public void LayoutForTree_NoMatchingAssignee_ReturnsEmpty()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Open).WithAssignedTo("john").Build();
        var result = NewService().LayoutForTree(new[] { issue }, assignedTo: "nonexistent");
        result.Nodes.Should().BeEmpty();
        result.TotalLanes.Should().Be(0);
    }

    [Test]
    public void LayoutForTree_HideMode_MatchesDefault()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();
        var svc = NewService();
        var hide = svc.LayoutForTree(new[] { open, complete }, visibility: InactiveVisibility.Hide);
        var def = svc.LayoutForTree(new[] { open, complete });
        hide.Nodes.Select(n => n.Node.Id).Should().BeEquivalentTo(def.Nodes.Select(n => n.Node.Id));
    }

    [Test]
    public void LayoutForNext_WithMatchedIds_IncludesMatchedAndAncestorContext()
    {
        var grandparent = new IssueBuilder().WithId("gp").WithStatus(IssueStatus.Open).Build();
        var parent = new IssueBuilder().WithId("p").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("gp", "aaa").Build();
        var matched = new IssueBuilder().WithId("m").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("p", "aaa").Build();
        var unrelated = new IssueBuilder().WithId("u").WithStatus(IssueStatus.Open).Build();

        var result = NewService().LayoutForNext(
            new[] { grandparent, parent, matched, unrelated },
            matchedIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "m" });

        result.Nodes.Select(n => n.Node.Id).Should().BeEquivalentTo(new[] { "m", "p", "gp" });
    }

    [Test]
    public void LayoutForNext_WithoutMatchedIds_FallsBackToTreeBehavior()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var result = NewService().LayoutForNext(new[] { open }, matchedIds: null);
        result.Nodes.Should().ContainSingle().Which.Node.Id.Should().Be("open1");
    }

    [Test]
    public void Layout_CycleInIssueGraph_ThrowsInvalidGraphException()
    {
        var a = new IssueBuilder().WithId("A").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("B", "aaa").Build();
        var b = new IssueBuilder().WithId("B").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("A", "aaa").Build();

        var svc = NewService();
        var act = () => svc.LayoutForTree(new[] { a, b });
        act.Should().Throw<InvalidGraphException>();
    }
}
