using System.Text;
using Fleece.Core.Models.Graph;

namespace Fleece.Core.Tests.Services.GraphLayout.Helpers;

/// <summary>
/// Test-only ASCII renderer for <see cref="GraphLayout{TNode}"/>. Walks the engine-provided
/// occupancy matrix and chooses Unicode box-drawing glyphs from each cell's edges and node.
/// Used to anchor Verify snapshots over <see cref="LayoutMode.NormalTree"/> output; not shipped
/// in <c>Fleece.Cli</c>.
/// </summary>
internal static class NormalTreeAsciiRenderer
{
    private const char OpenMarker = '○';

    public static string Render<TNode>(
        GraphLayout<TNode> layout,
        Func<TNode, char>? nodeMarker = null)
        where TNode : IGraphNode
    {
        if (layout.Nodes.Count == 0)
        {
            return string.Empty;
        }

        nodeMarker ??= _ => OpenMarker;

        int rows = layout.TotalRows;
        int lanes = layout.TotalLanes;
        var grid = new char[rows, lanes];
        for (int r = 0; r < rows; r++)
        {
            for (int l = 0; l < lanes; l++)
            {
                grid[r, l] = ' ';
            }
        }

        for (int r = 0; r < rows; r++)
        {
            for (int l = 0; l < lanes; l++)
            {
                var cell = layout.Occupancy[r, l];
                if (cell.Node is not null || cell.Edges.Count == 0)
                {
                    continue;
                }

                bool top = HasSharedEdge(layout.Occupancy, r, l, -1, 0);
                bool bottom = HasSharedEdge(layout.Occupancy, r, l, 1, 0);
                bool left = HasSharedEdge(layout.Occupancy, r, l, 0, -1);
                bool right = HasSharedEdge(layout.Occupancy, r, l, 0, 1);

                grid[r, l] = ChooseGlyph(top, bottom, left, right);
            }
        }

        foreach (var node in layout.Nodes)
        {
            grid[node.Row, node.Lane] = nodeMarker(node.Node);
        }

        var sb = new StringBuilder();
        for (int r = 0; r < rows; r++)
        {
            var rowChars = new char[lanes];
            for (int l = 0; l < lanes; l++)
            {
                rowChars[l] = grid[r, l];
            }
            string rowStr = new string(rowChars).TrimEnd();

            var node = FindNodeAtRow(layout, r);
            if (node is not null)
            {
                var suffix = node.TotalAppearances > 1
                    ? $" ({node.AppearanceIndex}/{node.TotalAppearances})"
                    : string.Empty;
                sb.AppendLine($"{rowStr}  {node.Node.Id}{suffix}");
            }
            else
            {
                sb.AppendLine(rowStr);
            }
        }
        return sb.ToString();
    }

    private static bool HasSharedEdge(OccupancyCell[,] occ, int r, int l, int dr, int dl)
    {
        int nr = r + dr;
        int nl = l + dl;
        int totalRows = occ.GetLength(0);
        int totalLanes = occ.GetLength(1);
        if (nr < 0 || nr >= totalRows || nl < 0 || nl >= totalLanes)
        {
            return false;
        }
        var cell = occ[r, l];
        var neighbour = occ[nr, nl];
        if (cell.Edges.Count == 0 || neighbour.Edges.Count == 0)
        {
            return false;
        }
        var ids = new HashSet<string>(cell.Edges.Select(e => e.EdgeId), StringComparer.Ordinal);
        return neighbour.Edges.Any(e => ids.Contains(e.EdgeId));
    }

    private static PositionedNode<TNode>? FindNodeAtRow<TNode>(GraphLayout<TNode> layout, int row)
        where TNode : IGraphNode
    {
        foreach (var n in layout.Nodes)
        {
            if (n.Row == row)
            {
                return n;
            }
        }
        return null;
    }

    private static char ChooseGlyph(bool top, bool bottom, bool left, bool right)
    {
        return (top, bottom, left, right) switch
        {
            (true, true, true, true) => '┼',
            (true, true, false, true) => '├',
            (true, true, true, false) => '┤',
            (true, false, true, true) => '┴',
            (false, true, true, true) => '┬',
            (true, true, false, false) => '│',
            (false, false, true, true) => '─',
            (true, false, false, true) => '└',
            (true, false, true, false) => '┘',
            (false, true, false, true) => '┌',
            (false, true, true, false) => '┐',
            (true, false, false, false) => '│',
            (false, true, false, false) => '│',
            (false, false, true, false) => '─',
            (false, false, false, true) => '─',
            _ => ' '
        };
    }
}
