using Fleece.Core.Models.Graph;
using Fleece.Core.Services.GraphLayout;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services.GraphLayout;

[TestFixture]
public class NormalTreeEdgeTests
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
    public void SeriesChain_OneCornerToParent_NMinusOneSiblings()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A" };
        var b = new TestNode { Id = "B" };
        var c = new TestNode { Id = "C" };
        p.Children.AddRange(new[] { a, b, c });

        var layout = Layout(new[] { p, a, b, c }, new[] { p });

        var corners = layout.Edges.Where(e => e.Kind == EdgeKind.SeriesCornerToParent).ToList();
        corners.Should().ContainSingle();
        corners[0].From.Id.Should().Be("P");
        corners[0].To.Id.Should().Be("A");
        corners[0].Start.Should().Be(new GridPosition(0, 0));
        corners[0].End.Should().Be(new GridPosition(1, 1));
        corners[0].PivotLane.Should().Be(0);

        var siblings = layout.Edges.Where(e => e.Kind == EdgeKind.SeriesSibling).ToList();
        siblings.Should().HaveCount(2);
        siblings.Should().Contain(e => e.From.Id == "A" && e.To.Id == "B");
        siblings.Should().Contain(e => e.From.Id == "B" && e.To.Id == "C");
        siblings.Should().AllSatisfy(e => e.PivotLane.Should().BeNull());
    }

    [Test]
    public void ParallelParent_NSpineEdges_PivotEqualsParentLane()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        var z = new TestNode { Id = "Z" };
        p.Children.AddRange(new[] { x, y, z });

        var layout = Layout(new[] { p, x, y, z }, new[] { p });

        var spine = layout.Edges.Where(e => e.Kind == EdgeKind.ParallelChildToSpine).ToList();
        spine.Should().HaveCount(3);

        spine.Should().AllSatisfy(e =>
        {
            e.From.Id.Should().Be("P");
            e.PivotLane.Should().Be(0);
            e.Start.Should().Be(new GridPosition(0, 0));
            e.End.Lane.Should().Be(1);
        });

        spine.Select(e => e.To.Id).Should().BeEquivalentTo(new[] { "X", "Y", "Z" });
    }

    [Test]
    public void AllNormalTreeEdges_HaveSpecCompliantAttachSides()
    {
        var pSeries = new TestNode { Id = "PS", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A" };
        var b = new TestNode { Id = "B" };
        pSeries.Children.AddRange(new[] { a, b });

        var pParallel = new TestNode { Id = "PP", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        pParallel.Children.Add(x);

        var layout = Layout(
            new[] { pSeries, a, b, pParallel, x },
            new[] { pSeries, pParallel });

        foreach (var edge in layout.Edges)
        {
            switch (edge.Kind)
            {
                case EdgeKind.SeriesSibling:
                    edge.SourceAttach.Should().Be(EdgeAttachSide.Bottom);
                    edge.TargetAttach.Should().Be(EdgeAttachSide.Top);
                    break;
                case EdgeKind.SeriesCornerToParent:
                    edge.SourceAttach.Should().Be(EdgeAttachSide.Bottom);
                    edge.TargetAttach.Should().Be(EdgeAttachSide.Left);
                    break;
                case EdgeKind.ParallelChildToSpine:
                    edge.SourceAttach.Should().Be(EdgeAttachSide.Bottom);
                    edge.TargetAttach.Should().Be(EdgeAttachSide.Left);
                    break;
            }
        }
    }

    [Test]
    public void EdgeIds_FollowFromArrowToColonKindHashAppearancePattern()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A" };
        var b = new TestNode { Id = "B" };
        p.Children.AddRange(new[] { a, b });

        var layout = Layout(new[] { p, a, b }, new[] { p });

        layout.Edges.Should().AllSatisfy(e =>
        {
            e.Id.Should().Contain("->");
            e.Id.Should().Contain($":{e.Kind}");
            e.Id.Should().Contain("#");
        });
        layout.Edges.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void FirstSeriesChildHasNoSeriesSiblingEdgeAsTarget()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A" };
        var b = new TestNode { Id = "B" };
        p.Children.AddRange(new[] { a, b });

        var layout = Layout(new[] { p, a, b }, new[] { p });

        layout.Edges.Should().NotContain(e =>
            e.Kind == EdgeKind.SeriesSibling && e.To.Id == "A");
    }
}
