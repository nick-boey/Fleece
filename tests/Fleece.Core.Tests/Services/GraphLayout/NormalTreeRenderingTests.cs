using Fleece.Core.Models.Graph;
using Fleece.Core.Services.GraphLayout;
using Fleece.Core.Tests.Services.GraphLayout.Helpers;
using NUnit.Framework;
using VerifyNUnit;

namespace Fleece.Core.Tests.Services.GraphLayout;

[TestFixture]
public class NormalTreeRenderingTests
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
    public Task NormalTreeRendering_SeriesChain()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A" };
        var b = new TestNode { Id = "B" };
        var c = new TestNode { Id = "C" };
        p.Children.AddRange(new[] { a, b, c });

        var layout = Layout(new[] { p, a, b, c }, new[] { p });
        var rendered = NormalTreeAsciiRenderer.Render(layout);
        return Verifier.Verify(rendered);
    }

    [Test]
    public Task NormalTreeRendering_ParallelFan()
    {
        var p = new TestNode { Id = "P", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        var z = new TestNode { Id = "Z" };
        p.Children.AddRange(new[] { x, y, z });

        var layout = Layout(new[] { p, x, y, z }, new[] { p });
        var rendered = NormalTreeAsciiRenderer.Render(layout);
        return Verifier.Verify(rendered);
    }

    [Test]
    public Task NormalTreeRendering_MixedDepth()
    {
        var root = new TestNode { Id = "ROOT", ChildSequencing = ChildSequencing.Series };
        var a = new TestNode { Id = "A", ChildSequencing = ChildSequencing.Parallel };
        var x = new TestNode { Id = "X" };
        var y = new TestNode { Id = "Y" };
        var b = new TestNode { Id = "B", ChildSequencing = ChildSequencing.Series };
        var b1 = new TestNode { Id = "B1" };
        var b2 = new TestNode { Id = "B2" };
        var c = new TestNode { Id = "C" };
        a.Children.AddRange(new[] { x, y });
        b.Children.AddRange(new[] { b1, b2 });
        root.Children.AddRange(new[] { a, b, c });

        var layout = Layout(new[] { root, a, b, c, x, y, b1, b2 }, new[] { root });
        var rendered = NormalTreeAsciiRenderer.Render(layout);
        return Verifier.Verify(rendered);
    }

    [Test]
    public Task NormalTreeRendering_MultiParent()
    {
        var pa = new TestNode { Id = "PA", ChildSequencing = ChildSequencing.Series };
        var pb = new TestNode { Id = "PB", ChildSequencing = ChildSequencing.Series };
        var shared = new TestNode { Id = "S" };
        pa.Children.Add(shared);
        pb.Children.Add(shared);

        var layout = Layout(new[] { pa, pb, shared }, new[] { pa, pb });
        var rendered = NormalTreeAsciiRenderer.Render(layout);
        return Verifier.Verify(rendered);
    }
}
