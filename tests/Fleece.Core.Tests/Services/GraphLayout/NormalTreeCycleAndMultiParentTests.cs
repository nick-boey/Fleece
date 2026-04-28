using Fleece.Core.Models.Graph;
using Fleece.Core.Services.GraphLayout;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services.GraphLayout;

[TestFixture]
public class NormalTreeCycleAndMultiParentTests
{
    private sealed class TestNode : IGraphNode
    {
        public required string Id { get; init; }
        public ChildSequencing ChildSequencing { get; init; } = ChildSequencing.Series;
        public List<TestNode> Children { get; } = new();
    }

    private static (GraphLayoutService engine, GraphLayoutRequest<TestNode> request) MakeRequest(
        IReadOnlyList<TestNode> all,
        IEnumerable<TestNode> roots)
    {
        var engine = new GraphLayoutService();
        var rootList = roots.ToList();
        var request = new GraphLayoutRequest<TestNode>
        {
            AllNodes = all,
            RootFinder = _ => rootList,
            ChildIterator = n => n.Children,
            Mode = LayoutMode.NormalTree
        };
        return (engine, request);
    }

    [Test]
    public void ThreeNodeCycle_ReturnsCycleDetected()
    {
        var a = new TestNode { Id = "A" };
        var b = new TestNode { Id = "B" };
        var c = new TestNode { Id = "C" };
        a.Children.Add(b);
        b.Children.Add(c);
        c.Children.Add(a);

        var (engine, request) = MakeRequest(new[] { a, b, c }, new[] { a });
        var result = engine.Layout(request);

        var cycle = (GraphLayoutResult<TestNode>.CycleDetected)result;
        cycle.Cycle.Should().Equal("A", "B", "C", "A");
    }

    [Test]
    public void SelfLoop_ReturnsCycleDetected()
    {
        var a = new TestNode { Id = "A" };
        a.Children.Add(a);

        var (engine, request) = MakeRequest(new[] { a }, new[] { a });
        var result = engine.Layout(request);

        var cycle = (GraphLayoutResult<TestNode>.CycleDetected)result;
        cycle.Cycle.Should().Equal("A", "A");
    }

    [Test]
    public void MultiParent_AppearsTwiceWithCorrectAppearanceCounts()
    {
        var pa = new TestNode { Id = "PA", ChildSequencing = ChildSequencing.Series };
        var pb = new TestNode { Id = "PB", ChildSequencing = ChildSequencing.Series };
        var shared = new TestNode { Id = "S" };
        pa.Children.Add(shared);
        pb.Children.Add(shared);

        var (engine, request) = MakeRequest(new[] { pa, pb, shared }, new[] { pa, pb });
        var layout = ((GraphLayoutResult<TestNode>.Success)engine.Layout(request)).Layout;

        var sharedNodes = layout.Nodes.Where(n => n.Node.Id == "S").ToList();
        sharedNodes.Should().HaveCount(2);
        sharedNodes[0].AppearanceIndex.Should().Be(1);
        sharedNodes[0].TotalAppearances.Should().Be(2);
        sharedNodes[1].AppearanceIndex.Should().Be(2);
        sharedNodes[1].TotalAppearances.Should().Be(2);
    }

    [Test]
    public void DiamondDependency_SharedDescendantAppearsTwice()
    {
        var a = new TestNode { Id = "A", ChildSequencing = ChildSequencing.Parallel };
        var b = new TestNode { Id = "B", ChildSequencing = ChildSequencing.Series };
        var c = new TestNode { Id = "C", ChildSequencing = ChildSequencing.Series };
        var d = new TestNode { Id = "D" };
        a.Children.AddRange(new[] { b, c });
        b.Children.Add(d);
        c.Children.Add(d);

        var (engine, request) = MakeRequest(new[] { a, b, c, d }, new[] { a });
        var layout = ((GraphLayoutResult<TestNode>.Success)engine.Layout(request)).Layout;

        var dNodes = layout.Nodes.Where(n => n.Node.Id == "D").ToList();
        dNodes.Should().HaveCount(2);
        dNodes.Should().AllSatisfy(n => n.TotalAppearances.Should().Be(2));
    }
}
