using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;

namespace Fleece.Cli.Tui.Screens;

/// <summary>
/// Displays the task graph visualization.
/// </summary>
public sealed class TaskGraphScreen(ITaskGraphService taskGraphService, IAnsiConsole console)
{
    // Box-drawing characters
    private const char Vertical = '\u2502';   // │
    private const char Horizontal = '\u2500'; // ─
    private const char TopLeft = '\u250C';    // ┌
    private const char TopRight = '\u2510';   // ┐
    private const char BottomLeft = '\u2514'; // └
    private const char BottomRight = '\u2518';// ┘
    private const char TeeRight = '\u251C';   // ├
    private const char TeeLeft = '\u2524';    // ┤
    private const char TeeDown = '\u252C';    // ┬
    private const char TeeUp = '\u2534';      // ┴
    private const char Cross = '\u253C';      // ┼

    // Node markers
    private const char ActionableMarker = '\u25CB';  // ○
    private const char OpenMarker = '\u25CC';        // ◌
    private const char CompleteMarker = '\u25CF';    // ●
    private const char ClosedMarker = '\u2298';      // ⊘

    public async Task ShowAsync(CancellationToken cancellationToken)
    {
        console.Clear();

        var graph = await taskGraphService.BuildGraphAsync(cancellationToken);

        if (graph.Nodes.Count == 0)
        {
            console.MarkupLine("[dim]No issues found for task graph.[/]");
            console.MarkupLine("[dim]Press any key to go back...[/]");
            console.Input.ReadKey(intercept: true);
            return;
        }

        var header = new Rule("[bold]Task Graph[/]");
        header.Style = Style.Parse("cyan");
        console.Write(header);
        console.WriteLine();

        RenderGraph(graph);

        console.WriteLine();
        console.MarkupLine("[dim]Legend:[/] \u25CB actionable  \u25CC open  \u25CF complete  \u2298 closed/archived");
        console.MarkupLine("[dim]Press any key to go back...[/]");
        console.Input.ReadKey(intercept: true);
    }

    private void RenderGraph(TaskGraph graph)
    {
        var nodeLookup = graph.Nodes.ToDictionary(n => n.Issue.Id, StringComparer.OrdinalIgnoreCase);

        int totalNodeRows = graph.Nodes.Count;
        int totalGridRows = totalNodeRows * 2 - 1;
        int totalGridCols = graph.TotalLanes * 2;

        if (totalGridCols == 0)
        {
            totalGridCols = 2;
        }

        var grid = new char[totalGridRows, totalGridCols];
        for (int r = 0; r < totalGridRows; r++)
        {
            for (int c = 0; c < totalGridCols; c++)
            {
                grid[r, c] = ' ';
            }
        }

        var connections = new bool[totalGridRows, totalGridCols, 4];

        // Place node markers
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            int gridRow = i * 2;
            int gridCol = node.Lane * 2;
            grid[gridRow, gridCol] = GetNodeMarker(node);
        }

        // Draw connectors
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            var parentNode = FindParentNodeInGraph(node, nodeLookup);

            if (parentNode == null)
            {
                continue;
            }

            int childGridRow = i * 2;
            int childCol = node.Lane * 2;
            int parentGridRow = parentNode.Row * 2;
            int parentCol = parentNode.Lane * 2;

            if (childCol == parentCol)
            {
                DrawVerticalSegment(grid, connections, childCol, childGridRow, parentGridRow);
            }
            else if (childCol < parentCol)
            {
                DrawHorizontalSegment(grid, connections, childGridRow, childCol, parentCol);
                DrawVerticalSegment(grid, connections, parentCol, childGridRow, parentGridRow);
            }
        }

        ResolveJunctions(grid, connections, totalGridRows, totalGridCols);

        // Re-place node markers
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            int gridRow = i * 2;
            int gridCol = node.Lane * 2;
            grid[gridRow, gridCol] = GetNodeMarker(node);
        }

        // Output
        for (int r = 0; r < totalGridRows; r++)
        {
            var graphPart = ExtractRow(grid, r, totalGridCols);

            if (r % 2 == 0)
            {
                int nodeIndex = r / 2;
                var node = graph.Nodes[nodeIndex];
                var statusColor = GetStatusColor(node.Issue.Status);
                var id = Markup.Escape(node.Issue.Id);
                var title = Markup.Escape(node.Issue.Title);
                console.MarkupLine($"{graphPart}  [{statusColor}]{id} {title}[/]");
            }
            else
            {
                console.WriteLine(graphPart);
            }
        }
    }

    private static TaskGraphNode? FindParentNodeInGraph(
        TaskGraphNode node,
        Dictionary<string, TaskGraphNode> nodeLookup)
    {
        if (node.Issue.ParentIssues.Count == 0)
        {
            return null;
        }

        TaskGraphNode? bestParent = null;
        foreach (var parentRef in node.Issue.ParentIssues)
        {
            if (nodeLookup.TryGetValue(parentRef.ParentIssue, out var parentNode))
            {
                if (bestParent == null || parentNode.Row > bestParent.Row)
                {
                    bestParent = parentNode;
                }
            }
        }

        return bestParent;
    }

    private static void DrawVerticalSegment(
        char[,] grid, bool[,,] connections, int col, int startRow, int endRow)
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

    private static void DrawHorizontalSegment(
        char[,] grid, bool[,,] connections, int row, int startCol, int endCol)
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

    private static void ResolveJunctions(
        char[,] grid, bool[,,] connections, int totalRows, int totalCols)
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

    private static char GetNodeMarker(TaskGraphNode node)
    {
        return node.Issue.Status switch
        {
            IssueStatus.Complete => CompleteMarker,
            IssueStatus.Deleted or IssueStatus.Archived or IssueStatus.Closed => ClosedMarker,
            _ => node.IsActionable ? ActionableMarker : OpenMarker
        };
    }

    private static string GetStatusColor(IssueStatus status)
    {
        return status switch
        {
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
