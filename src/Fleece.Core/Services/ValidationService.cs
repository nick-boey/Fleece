using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Service for validating issue data integrity.
/// </summary>
public class ValidationService : IValidationService
{
    private readonly IIssueService _issueService;

    public ValidationService(IIssueService issueService)
    {
        _issueService = issueService;
    }

    /// <inheritdoc />
    public async Task<DependencyValidationResult> ValidateDependencyCyclesAsync(CancellationToken ct = default)
    {
        var issues = await _issueService.GetAllAsync(ct);
        var cycles = DetectCycles(issues);
        return new DependencyValidationResult(cycles.Count == 0, cycles);
    }

    /// <inheritdoc />
    public async Task<bool> WouldCreateCycleAsync(string parentId, string childId, CancellationToken ct = default)
    {
        // Self-reference is always a cycle
        if (string.Equals(parentId, childId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // BFS from parentId following existing parent edges.
        // If we can reach childId, then adding childId -> parentId would create a cycle.
        var issues = await _issueService.GetAllAsync(ct);
        var parentMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in issues)
        {
            parentMap[issue.Id] = (issue.ParentIssues ?? []).Select(p => p.ParentIssue).ToList();
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(parentId);
        visited.Add(parentId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (parentMap.TryGetValue(current, out var parents))
            {
                foreach (var parent in parents)
                {
                    if (string.Equals(parent, childId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (visited.Add(parent))
                    {
                        queue.Enqueue(parent);
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Detects cycles in the ParentIssues dependency graph using DFS.
    /// </summary>
    private static List<DependencyCycle> DetectCycles(IEnumerable<Issue> issues)
    {
        var issueList = issues.ToList();

        // Build adjacency map: issue ID -> list of ParentIssues (dependencies)
        var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var issueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var issue in issueList)
        {
            issueIds.Add(issue.Id);
            adjacency[issue.Id] = (issue.ParentIssues ?? []).Select(p => p.ParentIssue).ToList();
        }

        // Node colors for DFS: White = unvisited, Gray = in current path, Black = fully processed
        var color = new Dictionary<string, NodeColor>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in issueIds)
        {
            color[id] = NodeColor.White;
        }

        var cycles = new List<DependencyCycle>();
        var detectedCycleSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var issue in issueList)
        {
            if (color[issue.Id] == NodeColor.White)
            {
                var path = new List<string>();
                Dfs(issue.Id, path, color, adjacency, issueIds, cycles, detectedCycleSignatures);
            }
        }

        return cycles;
    }

    private static void Dfs(
        string nodeId,
        List<string> path,
        Dictionary<string, NodeColor> color,
        Dictionary<string, List<string>> adjacency,
        HashSet<string> issueIds,
        List<DependencyCycle> cycles,
        HashSet<string> detectedCycleSignatures)
    {
        color[nodeId] = NodeColor.Gray;
        path.Add(nodeId);

        if (adjacency.TryGetValue(nodeId, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                // Skip dependencies that don't exist as issues (orphan references)
                if (!issueIds.Contains(dependency))
                {
                    continue;
                }

                if (color[dependency] == NodeColor.Gray)
                {
                    // Found a cycle! Extract it from the path
                    var cycleStartIndex = path.FindIndex(id =>
                        string.Equals(id, dependency, StringComparison.OrdinalIgnoreCase));

                    if (cycleStartIndex >= 0)
                    {
                        var cycleIds = path.Skip(cycleStartIndex).ToList();
                        cycleIds.Add(dependency); // Close the cycle

                        // Create a signature to avoid duplicate cycles
                        var signature = GetCycleSignature(cycleIds);
                        if (!detectedCycleSignatures.Contains(signature))
                        {
                            detectedCycleSignatures.Add(signature);
                            cycles.Add(new DependencyCycle(cycleIds));
                        }
                    }
                }
                else if (color[dependency] == NodeColor.White)
                {
                    Dfs(dependency, path, color, adjacency, issueIds, cycles, detectedCycleSignatures);
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        color[nodeId] = NodeColor.Black;
    }

    /// <summary>
    /// Creates a canonical signature for a cycle to detect duplicates.
    /// The signature is the sorted, joined cycle members (excluding the closing element).
    /// </summary>
    private static string GetCycleSignature(List<string> cycleIds)
    {
        // Take all but the last element (which is the duplicate closing the cycle)
        var cycleMembers = cycleIds.Take(cycleIds.Count - 1).ToList();

        // Find the lexicographically smallest rotation for canonical form
        var minRotation = cycleMembers;
        for (int i = 1; i < cycleMembers.Count; i++)
        {
            var rotation = cycleMembers.Skip(i).Concat(cycleMembers.Take(i)).ToList();
            if (string.Compare(string.Join(",", rotation), string.Join(",", minRotation), StringComparison.OrdinalIgnoreCase) < 0)
            {
                minRotation = rotation;
            }
        }

        return string.Join(",", minRotation).ToLowerInvariant();
    }

    private enum NodeColor
    {
        White,  // Unvisited
        Gray,   // In current DFS path
        Black   // Fully processed
    }
}
