using Fleece.Core.Models.Graph;
using Fleece.Core.Services.GraphLayout;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services.GraphLayout;

[TestFixture]
public class NormalTreeOccupancyTests
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
    public void ParallelSpineCells_ListEveryEdgePassingThrough()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        var z = new TestNode { Id = "Z" };
        p.Children.AddRange(new[] { x, y, z });

        var layout = Layout(new[] { p, x, y, z }, new[] { p });

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        int parentLane = byId["P"].Lane;
        int parentRow = byId["P"].Row;

        var spineCellAfterParent = layout.Occupancy[parentRow + 1, parentLane];
        spineCellAfterParent.Edges.Should().NotBeEmpty();

        var spineCellAtY = layout.Occupancy[byId["Y"].Row, parentLane];
        spineCellAtY.Edges.Count.Should().BeGreaterOrEqualTo(2,
            because: "Y's row at the spine has Y's corner cell plus Z's spine pass-through");
    }

    [Test]
    public void SeriesSiblingEdge_PassesThroughPreviousSubtreeRows()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A", ChildSequencing = ChildSequencing.Series };
        var x = new TestNode { Id = "X" };
        var b = new TestNode { Id = "B" };
        a.Children.Add(x);
        p.Children.AddRange(new[] { a, b });

        var layout = Layout(new[] { p, a, b, x }, new[] { p });

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        byId["P"].Should().BeEquivalentTo(new { Row = 0, Lane = 0 }, opts => opts.ExcludingMissingMembers());
        byId["A"].Should().BeEquivalentTo(new { Row = 1, Lane = 1 }, opts => opts.ExcludingMissingMembers());
        byId["X"].Should().BeEquivalentTo(new { Row = 2, Lane = 2 }, opts => opts.ExcludingMissingMembers());
        byId["B"].Should().BeEquivalentTo(new { Row = 3, Lane = 1 }, opts => opts.ExcludingMissingMembers());

        var sibling = layout.Edges.Single(e =>
            e.Kind == EdgeKind.SeriesSibling && e.From.Id == "A" && e.To.Id == "B");

        var passThroughCell = layout.Occupancy[2, 1];
        passThroughCell.Edges.Should().Contain(o =>
            o.EdgeId == sibling.Id && o.Segment == EdgeSegmentKind.Vertical);
    }

    [Test]
    public void ParentCell_HasNodeAndListsOutgoingEdges()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        p.Children.AddRange(new[] { x, y });

        var layout = Layout(new[] { p, x, y }, new[] { p });

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        var parentCell = layout.Occupancy[byId["P"].Row, byId["P"].Lane];

        parentCell.Node.Should().NotBeNull();
        parentCell.Node!.Node.Id.Should().Be("P");
        parentCell.Edges.Count.Should().Be(2,
            because: "two parallel spine edges start at the parent cell");
    }

    [Test]
    public void OccupancyDimensions_MatchTotalRowsAndLanes()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A" };
        var b = new TestNode { Id = "B" };
        p.Children.AddRange(new[] { a, b });

        var layout = Layout(new[] { p, a, b }, new[] { p });

        layout.Occupancy.GetLength(0).Should().Be(layout.TotalRows);
        layout.Occupancy.GetLength(1).Should().Be(layout.TotalLanes);
    }
}
