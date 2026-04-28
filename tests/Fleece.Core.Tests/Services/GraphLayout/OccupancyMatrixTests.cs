using Fleece.Core.Models.Graph;
using Fleece.Core.Services.GraphLayout;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services.GraphLayout;

[TestFixture]
public class OccupancyMatrixTests
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
            ChildIterator = n => n.Children
        };
        return ((GraphLayoutResult<TestNode>.Success)engine.Layout(request)).Layout;
    }

    [Test]
    public void MatrixDimensions_MatchTotalRowsAndLanes()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        p.Children.AddRange(new[] { x, y });

        var layout = Layout(new[] { p, x, y }, new[] { p });

        layout.Occupancy.GetLength(0).Should().Be(layout.TotalRows);
        layout.Occupancy.GetLength(1).Should().Be(layout.TotalLanes);
    }

    [Test]
    public void NodeCells_HaveNonNullNodeReference()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        p.Children.Add(x);

        var layout = Layout(new[] { p, x }, new[] { p });

        foreach (var n in layout.Nodes)
        {
            layout.Occupancy[n.Row, n.Lane].Node.Should().NotBeNull();
            layout.Occupancy[n.Row, n.Lane].Node!.Node.Id.Should().Be(n.Node.Id);
        }
    }

    [Test]
    public void ParallelSpine_SharedColumn_AggregatesEveryEdgePassingThrough()
    {
        // P has three parallel children — three spine edges share lane (parent.Lane).
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        var z = new TestNode { Id = "Z" };
        p.Children.AddRange(new[] { x, y, z });

        var layout = Layout(new[] { p, x, y, z }, new[] { p });

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        int parentLane = byId["P"].Lane;
        int parentRow = byId["P"].Row;

        // Cell directly above the parent must list every spine edge as a vertical segment.
        var aboveParentCell = layout.Occupancy[parentRow - 1, parentLane];
        aboveParentCell.Edges.Should().NotBeEmpty();
        aboveParentCell.Edges.Should().AllSatisfy(o => o.Segment.Should().Be(EdgeSegmentKind.Vertical));

        // The parent cell hosts the node AND lists incoming spine edges.
        var parentCell = layout.Occupancy[parentRow, parentLane];
        parentCell.Node.Should().NotBeNull();
        parentCell.Edges.Should().HaveCount(3);
    }

    [Test]
    public void SimpleSeriesChain_SiblingEdgePopulatesCellsAlongPath()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A", ChildSequencing = ChildSequencing.Series };
        var b = new TestNode { Id = "B", ChildSequencing = ChildSequencing.Series };
        var ga = new TestNode { Id = "GA" };
        var gb = new TestNode { Id = "GB" };
        a.Children.Add(ga);
        b.Children.Add(gb);
        p.Children.AddRange(new[] { a, b });

        var layout = Layout(new[] { p, a, b, ga, gb }, new[] { p });

        var sibling = layout.Edges.Single(e => e.Kind == EdgeKind.SeriesSibling);
        // The sibling edge's source cell must list the edge id.
        var srcCell = layout.Occupancy[sibling.Start.Row, sibling.Start.Lane];
        srcCell.Edges.Should().Contain(o => o.EdgeId == sibling.Id);

        var endCell = layout.Occupancy[sibling.End.Row, sibling.End.Lane];
        endCell.Edges.Should().Contain(o => o.EdgeId == sibling.Id);
    }
}
