namespace Fleece.Core.Models;

/// <summary>
/// Criteria for sorting lane 0 (actionable/root) issues in the task graph.
/// </summary>
public enum GraphSortCriteria
{
    CreatedAt,
    Priority,
    HasDescription,
    Title
}

/// <summary>
/// Direction for sorting.
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// A single sort rule pairing a criterion with a direction.
/// </summary>
public record GraphSortRule(GraphSortCriteria Criteria, SortDirection Direction = SortDirection.Ascending);

/// <summary>
/// Configuration for sorting lane 0 issues. Contains an ordered list of sort rules.
/// </summary>
public class GraphSortConfig
{
    public IReadOnlyList<GraphSortRule> Rules { get; init; } = [];

    /// <summary>
    /// Default sort: CreatedAt ascending (oldest first).
    /// </summary>
    public static GraphSortConfig Default { get; } = new()
    {
        Rules = [new GraphSortRule(GraphSortCriteria.CreatedAt)]
    };

    /// <summary>
    /// Parses a comma-separated sort string like "created,priority:desc,title:asc".
    /// </summary>
    public static GraphSortConfig Parse(string sortString)
    {
        var rules = new List<GraphSortRule>();

        foreach (var part in sortString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var segments = part.Split(':', 2);
            var criteriaStr = segments[0].Trim().ToLowerInvariant();
            var direction = SortDirection.Ascending;

            if (segments.Length > 1)
            {
                direction = segments[1].Trim().ToLowerInvariant() switch
                {
                    "desc" or "descending" => SortDirection.Descending,
                    "asc" or "ascending" => SortDirection.Ascending,
                    _ => throw new ArgumentException($"Invalid sort direction '{segments[1]}'. Use 'asc' or 'desc'.")
                };
            }

            var criteria = criteriaStr switch
            {
                "created" or "createdat" => GraphSortCriteria.CreatedAt,
                "priority" => GraphSortCriteria.Priority,
                "description" or "hasdescription" => GraphSortCriteria.HasDescription,
                "title" => GraphSortCriteria.Title,
                _ => throw new ArgumentException(
                    $"Invalid sort criteria '{criteriaStr}'. Valid options: created, priority, description, title.")
            };

            rules.Add(new GraphSortRule(criteria, direction));
        }

        return rules.Count == 0 ? Default : new GraphSortConfig { Rules = rules };
    }
}
