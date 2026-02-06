using Fleece.Core.Models;
using Spectre.Console;

namespace Fleece.Cli.Output;

/// <summary>
/// Renders a task graph to the console using Unicode box-drawing characters.
/// </summary>
public static class TaskGraphRenderer
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
    private const char ActionableMarker = 'o';
    private const char BlockedMarker = 'x';

    /// <summary>
    /// Renders a task graph to the console.
    /// </summary>
    public static void Render(TaskGraph graph)
    {
        if (graph.Nodes.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No issues found[/]");
            return;
        }

        // Build lookup structures
        var nodeLookup = graph.Nodes.ToDictionary(n => n.Issue.Id, StringComparer.OrdinalIgnoreCase);

        // Determine grid dimensions
        int totalNodeRows = graph.Nodes.Count;
        int totalGridRows = totalNodeRows * 2 - 1; // node rows + connector rows between them
        int totalGridCols = graph.TotalLanes * 2;   // each lane is 2 cols wide

        // Initialize grid with spaces
        var grid = new char[totalGridRows, totalGridCols];
        for (int r = 0; r < totalGridRows; r++)
        {
            for (int c = 0; c < totalGridCols; c++)
            {
                grid[r, c] = ' ';
            }
        }

        // Track connections at each cell for junction resolution
        // Each cell has flags: top, bottom, left, right
        var connections = new bool[totalGridRows, totalGridCols, 4]; // 0=top, 1=bottom, 2=left, 3=right

        // Place node markers and compute edges
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            int gridRow = i * 2;
            int gridCol = node.Lane * 2;

            // Place marker
            grid[gridRow, gridCol] = node.IsActionable ? ActionableMarker : BlockedMarker;
        }

        // Find parent node for each node and draw connectors
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
                // Same lane: vertical connector
                DrawVerticalSegment(grid, connections, childCol, childGridRow, parentGridRow);
            }
            else if (childCol < parentCol)
            {
                // Child is left of parent: horizontal on child's row, then vertical down
                DrawHorizontalSegment(grid, connections, childGridRow, childCol, parentCol);
                DrawVerticalSegment(grid, connections, parentCol, childGridRow, parentGridRow);
            }
        }

        // Resolve junction characters
        ResolveJunctions(grid, connections, totalGridRows, totalGridCols);

        // Re-place node markers (they take priority over junctions)
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            int gridRow = i * 2;
            int gridCol = node.Lane * 2;
            grid[gridRow, gridCol] = node.IsActionable ? ActionableMarker : BlockedMarker;
        }

        // Output the grid with issue titles
        for (int r = 0; r < totalGridRows; r++)
        {
            var graphPart = ExtractRow(grid, r, totalGridCols);

            if (r % 2 == 0)
            {
                // Node row - append issue title
                int nodeIndex = r / 2;
                var node = graph.Nodes[nodeIndex];
                var statusColor = GetStatusColor(node.Issue.Status);
                var title = Markup.Escape(node.Issue.Title);
                AnsiConsole.MarkupLine($"{graphPart}  [{statusColor}]{title}[/]");
            }
            else
            {
                // Connector row
                AnsiConsole.WriteLine(graphPart);
            }
        }
    }

    /// <summary>
    /// Finds the parent node of the given node within the graph.
    /// Returns the first parent that exists in the graph, preferring the parent
    /// that appears later (lower) in the node list.
    /// </summary>
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
                // Prefer the parent that appears later (higher row = further down the graph)
                if (bestParent == null || parentNode.Row > bestParent.Row)
                {
                    bestParent = parentNode;
                }
            }
        }

        return bestParent;
    }

    /// <summary>
    /// Draws a vertical segment between two rows at the given column.
    /// </summary>
    private static void DrawVerticalSegment(
        char[,] grid,
        bool[,,] connections,
        int col,
        int startRow,
        int endRow)
    {
        // Mark connection going down from start
        connections[startRow, col, 1] = true; // bottom

        // Mark connection going up to end
        connections[endRow, col, 0] = true; // top

        // Fill intermediate rows
        for (int r = startRow + 1; r < endRow; r++)
        {
            connections[r, col, 0] = true; // top
            connections[r, col, 1] = true; // bottom
            if (grid[r, col] == ' ')
            {
                grid[r, col] = Vertical;
            }
        }
    }

    /// <summary>
    /// Draws a horizontal segment between two columns on the given row.
    /// </summary>
    private static void DrawHorizontalSegment(
        char[,] grid,
        bool[,,] connections,
        int row,
        int startCol,
        int endCol)
    {
        // Mark connection going right from start
        connections[row, startCol, 3] = true; // right

        // Mark connection going left to end
        connections[row, endCol, 2] = true; // left

        // Fill intermediate columns
        for (int c = startCol + 1; c < endCol; c++)
        {
            connections[row, c, 2] = true; // left
            connections[row, c, 3] = true; // right
            if (grid[row, c] == ' ')
            {
                grid[row, c] = Horizontal;
            }
        }
    }

    /// <summary>
    /// Resolves junction characters at cells where multiple connections meet.
    /// </summary>
    private static void ResolveJunctions(
        char[,] grid,
        bool[,,] connections,
        int totalRows,
        int totalCols)
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
                    continue; // Single-direction connections are already drawn
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

    /// <summary>
    /// Extracts a row from the grid as a trimmed string.
    /// </summary>
    private static string ExtractRow(char[,] grid, int row, int totalCols)
    {
        var chars = new char[totalCols];
        for (int c = 0; c < totalCols; c++)
        {
            chars[c] = grid[row, c];
        }

        return new string(chars).TrimEnd();
    }

    /// <summary>
    /// Gets the Spectre.Console color name for an issue status.
    /// </summary>
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
