using Fleece.Core.Models;
using Fleece.Core.Models.Graph;
using Spectre.Console;

namespace Fleece.Cli.Output;

/// <summary>
/// Renders a <see cref="GraphLayout{Issue}"/> to the console using Unicode box-drawing characters.
/// Walks the engine-provided occupancy matrix; does not infer edges from positions.
/// </summary>
public static class TaskGraphRenderer
{
    private const char Vertical = '│';
    private const char Horizontal = '─';
    private const char TopLeft = '┌';
    private const char TopRight = '┐';
    private const char BottomLeft = '└';
    private const char BottomRight = '┘';
    private const char TeeRight = '├';
    private const char TeeLeft = '┤';
    private const char TeeDown = '┬';
    private const char TeeUp = '┴';
    private const char Cross = '┼';

    private const char ActionableMarker = '○';
    private const char OpenMarker = '◌';
    private const char CompleteMarker = '●';
    private const char ClosedMarker = '⊘';

    public static void Render(
        IAnsiConsole console,
        GraphLayout<Issue> graph,
        IReadOnlySet<string>? actionableIds = null,
        IReadOnlySet<string>? matchedIds = null)
    {
        if (graph.Nodes.Count == 0)
        {
            console.MarkupLine("[dim]No issues found[/]");
            return;
        }

        actionableIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int totalNodeRows = graph.Nodes.Count;
        int totalGridRows = totalNodeRows * 2 - 1;
        int totalGridCols = graph.TotalLanes * 2;

        var displayGrid = new char[totalGridRows, totalGridCols];
        for (int r = 0; r < totalGridRows; r++)
        {
            for (int c = 0; c < totalGridCols; c++)
            {
                displayGrid[r, c] = ' ';
            }
        }

        var connections = new bool[totalGridRows, totalGridCols, 4]; // top, bottom, left, right

        foreach (var edge in graph.Edges)
        {
            DrawEdge(displayGrid, connections, edge);
        }

        ResolveJunctions(displayGrid, connections, totalGridRows, totalGridCols);

        // Place node markers (override any junction at node cells)
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            int gridRow = node.Row * 2;
            int gridCol = node.Lane * 2;
            displayGrid[gridRow, gridCol] = GetNodeMarker(node, actionableIds);
        }

        for (int r = 0; r < totalGridRows; r++)
        {
            var graphPart = ExtractRow(displayGrid, r, totalGridCols);

            if (r % 2 == 0)
            {
                int nodeIndex = r / 2;
                var node = graph.Nodes[nodeIndex];
                var id = Markup.Escape(node.Node.Id);
                var title = Markup.Escape(node.Node.Title);
                var appearanceSuffix = node.TotalAppearances > 1
                    ? $" ({node.AppearanceIndex}/{node.TotalAppearances})"
                    : "";

                bool isContextOnly = matchedIds is not null &&
                                     matchedIds.Count > 0 &&
                                     !matchedIds.Contains(node.Node.Id);

                if (isContextOnly)
                {
                    console.MarkupLine($"{graphPart}  [dim]{id} {title}{appearanceSuffix}[/]");
                }
                else
                {
                    var statusColor = GetStatusColor(node.Node.Status);
                    var suffix = appearanceSuffix.Length > 0 ? $" [dim]{appearanceSuffix}[/]" : "";
                    console.MarkupLine($"{graphPart}  [{statusColor}]{id} {title}[/]{suffix}");
                }
            }
            else
            {
                console.WriteLine(graphPart);
            }
        }
    }

    private static void DrawEdge(char[,] grid, bool[,,] connections, Edge<Issue> edge)
    {
        int startGridRow = edge.Start.Row * 2;
        int startGridCol = edge.Start.Lane * 2;
        int endGridRow = edge.End.Row * 2;
        int endGridCol = edge.End.Lane * 2;

        switch (edge.Kind)
        {
            case EdgeKind.SeriesSibling:
            case EdgeKind.SeriesCornerToParent:
            {
                // vertical down, then horizontal across
                int pivotCol = startGridCol;
                if (endGridRow > startGridRow)
                {
                    DrawVerticalSegment(grid, connections, pivotCol, startGridRow, endGridRow);
                }
                if (endGridCol != pivotCol)
                {
                    int loCol = Math.Min(pivotCol, endGridCol);
                    int hiCol = Math.Max(pivotCol, endGridCol);
                    DrawHorizontalSegment(grid, connections, endGridRow, loCol, hiCol);
                }
                break;
            }
            case EdgeKind.ParallelChildToSpine:
            {
                // The same edge kind appears in two layout modes with opposite
                // geometry: in IssueGraph the source is a child branching right to
                // a parent spine (horizontal-then-vertical), while in NormalTree
                // the source is a parent branching down to each child via its own
                // corner (vertical-then-horizontal). SourceAttach disambiguates —
                // Bottom means the edge leaves the source going down.
                if (edge.SourceAttach == EdgeAttachSide.Bottom)
                {
                    int pivotCol = startGridCol;
                    if (endGridRow > startGridRow)
                    {
                        DrawVerticalSegment(grid, connections, pivotCol, startGridRow, endGridRow);
                    }
                    if (endGridCol != pivotCol)
                    {
                        int loCol = Math.Min(pivotCol, endGridCol);
                        int hiCol = Math.Max(pivotCol, endGridCol);
                        DrawHorizontalSegment(grid, connections, endGridRow, loCol, hiCol);
                    }
                }
                else
                {
                    int pivotCol = endGridCol;
                    if (pivotCol != startGridCol)
                    {
                        int loCol = Math.Min(pivotCol, startGridCol);
                        int hiCol = Math.Max(pivotCol, startGridCol);
                        DrawHorizontalSegment(grid, connections, startGridRow, loCol, hiCol);
                    }
                    if (endGridRow > startGridRow)
                    {
                        DrawVerticalSegment(grid, connections, pivotCol, startGridRow, endGridRow);
                    }
                }
                break;
            }
        }
    }

    private static void DrawVerticalSegment(char[,] grid, bool[,,] connections, int col, int startRow, int endRow)
    {
        connections[startRow, col, 1] = true;
        connections[endRow, col, 0] = true;
        for (int r = startRow + 1; r < endRow; r++)
        {
            connections[r, col, 0] = true;
            connections[r, col, 1] = true;
            if (grid[r, col] == ' ')
            {
                grid[r, col] = Vertical;
            }
        }
    }

    private static void DrawHorizontalSegment(char[,] grid, bool[,,] connections, int row, int startCol, int endCol)
    {
        connections[row, startCol, 3] = true;
        connections[row, endCol, 2] = true;
        for (int c = startCol + 1; c < endCol; c++)
        {
            connections[row, c, 2] = true;
            connections[row, c, 3] = true;
            if (grid[row, c] == ' ')
            {
                grid[row, c] = Horizontal;
            }
        }
    }

    private static void ResolveJunctions(char[,] grid, bool[,,] connections, int totalRows, int totalCols)
    {
        for (int r = 0; r < totalRows; r++)
        {
            for (int c = 0; c < totalCols; c++)
            {
                bool top = connections[r, c, 0];
                bool bottom = connections[r, c, 1];
                bool left = connections[r, c, 2];
                bool right = connections[r, c, 3];

                int count = (top ? 1 : 0) + (bottom ? 1 : 0) + (left ? 1 : 0) + (right ? 1 : 0);
                if (count < 2)
                {
                    continue;
                }

                grid[r, c] = (top, bottom, left, right) switch
                {
                    (true, true, true, true) => Cross,
                    (true, true, false, true) => TeeRight,
                    (true, true, true, false) => TeeLeft,
                    (true, false, true, true) => TeeUp,
                    (false, true, true, true) => TeeDown,
                    (true, true, false, false) => Vertical,
                    (false, false, true, true) => Horizontal,
                    (true, false, false, true) => BottomLeft,
                    (true, false, true, false) => BottomRight,
                    (false, true, false, true) => TopLeft,
                    (false, true, true, false) => TopRight,
                    _ => grid[r, c]
                };
            }
        }
    }

    private static string ExtractRow(char[,] grid, int row, int totalCols)
    {
        var chars = new char[totalCols];
        for (int c = 0; c < totalCols; c++)
        {
            chars[c] = grid[row, c];
        }
        return new string(chars).TrimEnd();
    }

    private static char GetNodeMarker(PositionedNode<Issue> node, IReadOnlySet<string> actionableIds)
    {
        return node.Node.Status switch
        {
            IssueStatus.Complete => CompleteMarker,
            IssueStatus.Deleted or IssueStatus.Archived or IssueStatus.Closed => ClosedMarker,
            _ => actionableIds.Contains(node.Node.Id) ? ActionableMarker : OpenMarker
        };
    }

    private static string GetStatusColor(IssueStatus status)
    {
        return status switch
        {
            IssueStatus.Draft => "dim",
            IssueStatus.Open => "cyan",
            IssueStatus.Progress => "blue",
            IssueStatus.Review => "purple",
            IssueStatus.Complete => "green",
            IssueStatus.Archived => "dim",
            IssueStatus.Closed => "dim",
            _ => "white"
        };
    }
}
