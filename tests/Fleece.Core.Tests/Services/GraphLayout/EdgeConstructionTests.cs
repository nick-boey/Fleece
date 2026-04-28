using Fleece.Core.Models.Graph;
using Fleece.Core.Services.GraphLayout;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services.GraphLayout;

[TestFixture]
public class EdgeConstructionTests
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
    public void SeriesParent_NestedChildren_ProducesSiblingAndCornerEdges()
    {
        // Three series children, each with one grandchild so they're laid out as subtrees (advance lanes).
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A", ChildSequencing = ChildSequencing.Series };
        var b = new TestNode { Id = "B", ChildSequencing = ChildSequencing.Series };
        var c = new TestNode { Id = "C", ChildSequencing = ChildSequencing.Series };
        var ga = new TestNode { Id = "GA" };
        var gb = new TestNode { Id = "GB" };
        var gc = new TestNode { Id = "GC" };
        a.Children.Add(ga);
        b.Children.Add(gb);
        c.Children.Add(gc);
        p.Children.AddRange(new[] { a, b, c });

        var layout = Layout(new[] { p, a, b, c, ga, gb, gc }, new[] { p });

        // Subtree roots (the child nodes themselves placed at maxLane+1) get the SeriesSibling edges.
        var siblingEdges = layout.Edges.Where(e => e.Kind == EdgeKind.SeriesSibling).ToList();
        siblingEdges.Should().HaveCount(2);
        siblingEdges.Should().Contain(e => e.From.Id == "A" && e.To.Id == "B");
        siblingEdges.Should().Contain(e => e.From.Id == "B" && e.To.Id == "C");

        var corner = layout.Edges.Single(e => e.Kind == EdgeKind.SeriesCornerToParent && e.To.Id == "P");
        corner.From.Id.Should().Be("C");
        corner.PivotLane.Should().Be(corner.Start.Lane);

        siblingEdges.Should().AllSatisfy(e =>
        {
            e.SourceAttach.Should().Be(EdgeAttachSide.Bottom);
            e.TargetAttach.Should().Be(EdgeAttachSide.Top);
        });
        corner.SourceAttach.Should().Be(EdgeAttachSide.Bottom);
        corner.TargetAttach.Should().Be(EdgeAttachSide.Left);
    }

    [Test]
    public void ParallelParent_ChildrenAllProduceSpineEdges_PivotAtParentLane()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        var z = new TestNode { Id = "Z" };
        p.Children.AddRange(new[] { x, y, z });

        var layout = Layout(new[] { p, x, y, z }, new[] { p });

        var spine = layout.Edges.Where(e => e.Kind == EdgeKind.ParallelChildToSpine).ToList();
        spine.Should().HaveCount(3);

        var byId = layout.Nodes.ToDictionary(n => n.Node.Id);
        var parentLane = byId["P"].Lane;

        spine.Should().AllSatisfy(e =>
        {
            e.PivotLane.Should().Be(parentLane);
            e.End.Lane.Should().Be(parentLane);
            e.SourceAttach.Should().Be(EdgeAttachSide.Right);
            e.TargetAttach.Should().Be(EdgeAttachSide.Top);
        });

        spine.Select(e => e.From.Id).Should().BeEquivalentTo(new[] { "X", "Y", "Z" });
        spine.Should().AllSatisfy(e => e.To.Id.Should().Be("P"));
    }

    [Test]
    public void FirstSeriesChild_HasNoIncomingEdge()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A" };
        var b = new TestNode { Id = "B" };
        p.Children.AddRange(new[] { a, b });

        var layout = Layout(new[] { p, a, b }, new[] { p });

        layout.Edges.Should().NotContain(e => e.To.Id == "A");
    }

    [Test]
    public void EdgeIds_AreUniqueAcrossLayout()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        p.Children.AddRange(new[] { x, y });

        var layout = Layout(new[] { p, x, y }, new[] { p });

        layout.Edges.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }
}
