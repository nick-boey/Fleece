using Fleece.Core.Models.Graph;
using Fleece.Core.Services.GraphLayout;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services.GraphLayout;

[TestFixture]
public class GraphLayoutServiceTests
{
    private sealed class TestNode : IGraphNode
    {
        public required string Id { get; init; }
        public ChildSequencing ChildSequencing { get; init; } = ChildSequencing.Series;
        public List<TestNode> Children { get; } = new();
    }

    private static (GraphLayoutService engine, GraphLayoutRequest<TestNode> request) MakeRequest(
        IReadOnlyList<TestNode> all,
        IEnumerable<TestNode> roots,
        LayoutMode mode = LayoutMode.IssueGraph)
    {
        var engine = new GraphLayoutService();
        var rootList = roots.ToList();
        var request = new GraphLayoutRequest<TestNode>
        {
            AllNodes = all,
            RootFinder = _ => rootList,
            ChildIterator = n => n.Children,
            Mode = mode
        };
        return (engine, request);
    }

    private static GraphLayout<TestNode> Layout(IReadOnlyList<TestNode> all, IEnumerable<TestNode> roots)
    {
        var (engine, request) = MakeRequest(all, roots);
        var result = engine.Layout(request);
        return ((GraphLayoutResult<TestNode>.Success)result).Layout;
    }

    [Test]
    public void EmptyInput_ReturnsEmptyLayout()
    {
        var engine = new GraphLayoutService();
        var request = new GraphLayoutRequest<TestNode>
        {
            AllNodes = Array.Empty<TestNode>(),
            RootFinder = _ => Array.Empty<TestNode>(),
            ChildIterator = _ => Array.Empty<TestNode>()
        };

        var result = engine.Layout(request);
        var success = (GraphLayoutResult<TestNode>.Success)result;
        success.Layout.Nodes.Should().BeEmpty();
        success.Layout.TotalLanes.Should().Be(0);
        success.Layout.TotalRows.Should().Be(0);
    }

    [Test]
    public void SingleLeaf_PlacedAtRowZeroLaneZero()
    {
        var node = new TestNode { Id = "a" };
        var layout = Layout(new[] { node }, new[] { node });

        layout.Nodes.Should().ContainSingle();
        layout.Nodes[0].Row.Should().Be(0);
        layout.Nodes[0].Lane.Should().Be(0);
        layout.TotalLanes.Should().Be(1);
        layout.TotalRows.Should().Be(1);
    }

    [Test]
    public void SeriesParent_LeafChildren_AllChildrenAtLaneZero_ParentAtLaneOne()
    {
        var parent = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var c1 = new TestNode { Id = "A" };
        var c2 = new TestNode { Id = "B" };
        parent.Children.AddRange(new[] { c1, c2 });

        var layout = Layout(new[] { parent, c1, c2 }, new[] { parent });

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        byId["A"].Lane.Should().Be(0);
        byId["B"].Lane.Should().Be(0);
        byId["P"].Lane.Should().Be(1);
        layout.Nodes.Select(n => n.Node.Id).Should().ContainInOrder("A", "B", "P");
    }

    [Test]
    public void SeriesParent_NestedChildren_LaneAdvancesAroundSubtrees()
    {
        // Mirror BuildTaskGraphLayout_GoToWorkExample
        var goToWork = new TestNode { Id = "go-to-work", ChildSequencing = ChildSequencing.Series };
        var wakeUp = new TestNode { Id = "wake-up" };
        var makeBreakfast = new TestNode { Id = "make-breakfast", ChildSequencing = ChildSequencing.Parallel };
        var makeCoffee = new TestNode { Id = "make-coffee" };
        var makeToast = new TestNode { Id = "make-toast", ChildSequencing = ChildSequencing.Series };
        var toastBread = new TestNode { Id = "toast-bread" };
        var spreadButter = new TestNode { Id = "spread-butter" };
        var getInCar = new TestNode { Id = "get-in-car" };
        var driveToWork = new TestNode { Id = "drive-to-work" };

        goToWork.Children.AddRange(new[] { wakeUp, makeBreakfast, getInCar, driveToWork });
        makeBreakfast.Children.AddRange(new[] { makeCoffee, makeToast });
        makeToast.Children.AddRange(new[] { toastBread, spreadButter });

        var all = new[] { goToWork, wakeUp, makeBreakfast, makeCoffee, makeToast, toastBread, spreadButter, getInCar, driveToWork };
        var layout = Layout(all, new[] { goToWork });

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        byId["wake-up"].Lane.Should().Be(0);
        byId["make-coffee"].Lane.Should().Be(1);
        byId["toast-bread"].Lane.Should().Be(1);
        byId["spread-butter"].Lane.Should().Be(1);
        byId["make-toast"].Lane.Should().Be(2);
        byId["make-breakfast"].Lane.Should().Be(3);
        byId["get-in-car"].Lane.Should().Be(3);
        byId["drive-to-work"].Lane.Should().Be(3);
        byId["go-to-work"].Lane.Should().Be(4);
        layout.TotalLanes.Should().Be(5);
    }

    [Test]
    public void ParallelParent_LeafChildren_ChildrenStackAtSameLane()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        var z = new TestNode { Id = "Z" };
        p.Children.AddRange(new[] { x, y, z });

        var layout = Layout(new[] { p, x, y, z }, new[] { p });

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        byId["X"].Lane.Should().Be(0);
        byId["Y"].Lane.Should().Be(0);
        byId["Z"].Lane.Should().Be(0);
        byId["P"].Lane.Should().Be(1);
    }

    [Test]
    public void MultiParent_ChildAppearsUnderEachParent_WithAppearanceCounts()
    {
        var pa = new TestNode { Id = "PA", ChildSequencing = ChildSequencing.Series };
        var pb = new TestNode { Id = "PB", ChildSequencing = ChildSequencing.Series };
        var shared = new TestNode { Id = "S" };
        pa.Children.Add(shared);
        pb.Children.Add(shared);

        var layout = Layout(new[] { pa, pb, shared }, new[] { pa, pb });

        var sharedNodes = layout.Nodes.Where(n => n.Node.Id == "S").ToList();
        sharedNodes.Should().HaveCount(2);
        sharedNodes[0].AppearanceIndex.Should().Be(1);
        sharedNodes[0].TotalAppearances.Should().Be(2);
        sharedNodes[1].AppearanceIndex.Should().Be(2);
        sharedNodes[1].TotalAppearances.Should().Be(2);
    }

    [Test]
    public void DiamondDependency_SharedDescendantAppearsTwice_NoCycle()
    {
        var a = new TestNode { Id = "A", ChildSequencing = ChildSequencing.Parallel };
        var b = new TestNode { Id = "B", ChildSequencing = ChildSequencing.Series };
        var c = new TestNode { Id = "C", ChildSequencing = ChildSequencing.Series };
        var d = new TestNode { Id = "D" };
        a.Children.AddRange(new[] { b, c });
        b.Children.Add(d);
        c.Children.Add(d);

        var layout = Layout(new[] { a, b, c, d }, new[] { a });

        var dNodes = layout.Nodes.Where(n => n.Node.Id == "D").ToList();
        dNodes.Should().HaveCount(2);
        dNodes[0].TotalAppearances.Should().Be(2);
    }

    [Test]
    public void ThreeNodeCycle_ReturnsCycleDetected()
    {
        var a = new TestNode { Id = "A" };
        var b = new TestNode { Id = "B" };
        var c = new TestNode { Id = "C" };
        a.Children.Add(b);
        b.Children.Add(c);
        c.Children.Add(a); // cycle

        var (engine, request) = MakeRequest(new[] { a, b, c }, new[] { a });
        var result = engine.Layout(request);

        var cycle = (GraphLayoutResult<TestNode>.CycleDetected)result;
        cycle.Cycle.Should().Equal("A", "B", "C", "A");
    }

    [Test]
    public void SelfLoop_ReturnsCycleDetectedWithSingletonRotation()
    {
        var a = new TestNode { Id = "A" };
        a.Children.Add(a);

        var (engine, request) = MakeRequest(new[] { a }, new[] { a });
        var result = engine.Layout(request);

        var cycle = (GraphLayoutResult<TestNode>.CycleDetected)result;
        cycle.Cycle.Should().Equal("A", "A");
    }

    [Test]
    public void NormalTreeMode_ThrowsNotImplemented()
    {
        var engine = new GraphLayoutService();
        var request = new GraphLayoutRequest<TestNode>
        {
            AllNodes = Array.Empty<TestNode>(),
            RootFinder = _ => Array.Empty<TestNode>(),
            ChildIterator = _ => Array.Empty<TestNode>(),
            Mode = LayoutMode.NormalTree
        };

        var act = () => engine.Layout(request);
        act.Should().Throw<NotImplementedException>()
            .WithMessage("*LayoutMode.NormalTree*");
    }
}
