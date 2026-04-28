using Fleece.Core.Models.Graph;
using Fleece.Core.Services.GraphLayout;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services.GraphLayout;

[TestFixture]
public class NormalTreeLayoutTests
{
    private sealed class TestNode : IGraphNode
    {
        public required string Id { get; init; }
        public ChildSequencing ChildSequencing { get; init; } = ChildSequencing.Series;
        public List<TestNode> Children { get; } = new();
    }

    private static GraphLayout<TestNode> Layout(IReadOnlyList<TestNode> all, IEnumerable<TestNode> roots)
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
        return ((GraphLayoutResult<TestNode>.Success)engine.Layout(request)).Layout;
    }

    [Test]
    public void SingleNode_RootAtRowZeroLaneZero()
    {
        var p = new TestNode { Id = "P" };
        var layout = Layout(new[] { p }, new[] { p });

        layout.Nodes.Should().ContainSingle();
        layout.Nodes[0].Row.Should().Be(0);
        layout.Nodes[0].Lane.Should().Be(0);
    }

    [Test]
    public void PreOrderRowAssignment_ParentBeforeDescendants()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A", ChildSequencing = ChildSequencing.Series };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        var b = new TestNode { Id = "B" };
        a.Children.AddRange(new[] { x, y });
        p.Children.AddRange(new[] { a, b });

        var layout = Layout(new[] { p, a, b, x, y }, new[] { p });

        layout.Nodes.Select(n => n.Node.Id).Should().ContainInOrder("P", "A", "X", "Y", "B");
    }

    [Test]
    public void EveryChildLaneEqualsParentLanePlusOne()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A", ChildSequencing = ChildSequencing.Series };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        var b = new TestNode { Id = "B" };
        a.Children.AddRange(new[] { x, y });
        p.Children.AddRange(new[] { a, b });

        var layout = Layout(new[] { p, a, b, x, y }, new[] { p });

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        byId["P"].Lane.Should().Be(0);
        byId["A"].Lane.Should().Be(1);
        byId["B"].Lane.Should().Be(1);
        byId["X"].Lane.Should().Be(2);
        byId["Y"].Lane.Should().Be(2);
    }

    [Test]
    public void SeriesChildren_ShareParentLanePlusOne_NoLaneAdvancement()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A" };
        var b = new TestNode { Id = "B" };
        var c = new TestNode { Id = "C" };
        p.Children.AddRange(new[] { a, b, c });

        var layout = Layout(new[] { p, a, b, c }, new[] { p });

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        byId["P"].Row.Should().Be(0);
        byId["P"].Lane.Should().Be(0);
        byId["A"].Should().BeEquivalentTo(new { Row = 1, Lane = 1 }, opts => opts.ExcludingMissingMembers());
        byId["B"].Should().BeEquivalentTo(new { Row = 2, Lane = 1 }, opts => opts.ExcludingMissingMembers());
        byId["C"].Should().BeEquivalentTo(new { Row = 3, Lane = 1 }, opts => opts.ExcludingMissingMembers());
    }

    [Test]
    public void ParallelChildren_ShareParentLanePlusOne()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        var z = new TestNode { Id = "Z" };
        p.Children.AddRange(new[] { x, y, z });

        var layout = Layout(new[] { p, x, y, z }, new[] { p });

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        byId["X"].Lane.Should().Be(1);
        byId["Y"].Lane.Should().Be(1);
        byId["Z"].Lane.Should().Be(1);
        byId["X"].Row.Should().Be(1);
        byId["Y"].Row.Should().Be(2);
        byId["Z"].Row.Should().Be(3);
    }

    [Test]
    public void SubtreesEmitContiguously_BAfterAllOfASubtree()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A", ChildSequencing = ChildSequencing.Series };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        var b = new TestNode { Id = "B" };
        a.Children.AddRange(new[] { x, y });
        p.Children.AddRange(new[] { a, b });

        var layout = Layout(new[] { p, a, b, x, y }, new[] { p });

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        var aSubtreeRows = new[] { byId["A"].Row, byId["X"].Row, byId["Y"].Row };
        byId["B"].Row.Should().BeGreaterThan(aSubtreeRows.Max());
    }

    [Test]
    public void MultipleRoots_AllPlacedAtLaneZero_ContiguousRowsPerSubtree()
    {
        var r1 = new TestNode { Id = "R1", ChildSequencing = ChildSequencing.Series };
        var r2 = new TestNode { Id = "R2" };
        var c1 = new TestNode { Id = "C1" };
        r1.Children.Add(c1);

        var layout = Layout(new[] { r1, r2, c1 }, new[] { r1, r2 });

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        byId["R1"].Lane.Should().Be(0);
        byId["R2"].Lane.Should().Be(0);
        byId["R1"].Row.Should().Be(0);
        byId["C1"].Row.Should().Be(1);
        byId["R2"].Row.Should().Be(2);
    }

    [Test]
    public void NormalTree_NoLongerThrows_ReturnsSuccess()
    {
        var p = new TestNode { Id = "P" };
        var engine = new GraphLayoutService();
        var request = new GraphLayoutRequest<TestNode>
        {
            AllNodes = new[] { p },
            RootFinder = _ => new[] { p },
            ChildIterator = n => n.Children,
            Mode = LayoutMode.NormalTree
        };

        var result = engine.Layout(request);
        result.Should().BeOfType<GraphLayoutResult<TestNode>.Success>();
    }
}
