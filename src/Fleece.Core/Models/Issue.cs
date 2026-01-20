namespace Fleece.Core.Models;

public sealed record Issue
{
    public required string Id { get; init; }

    public required string Title { get; init; }
    public DateTimeOffset TitleLastUpdate { get; init; }

    public string? Description { get; init; }
    public DateTimeOffset? DescriptionLastUpdate { get; init; }

    public required IssueStatus Status { get; init; }
    public DateTimeOffset StatusLastUpdate { get; init; }

    public required IssueType Type { get; init; }
    public DateTimeOffset TypeLastUpdate { get; init; }

    public int? LinkedPR { get; init; }
    public DateTimeOffset? LinkedPRLastUpdate { get; init; }

    public IReadOnlyList<string> LinkedIssues { get; init; } = [];
    public DateTimeOffset LinkedIssuesLastUpdate { get; init; }

    public IReadOnlyList<string> ParentIssues { get; init; } = [];
    public DateTimeOffset ParentIssuesLastUpdate { get; init; }

    public int? Priority { get; init; }
    public DateTimeOffset? PriorityLastUpdate { get; init; }

    public string? Group { get; init; }
    public DateTimeOffset? GroupLastUpdate { get; init; }

    public string? AssignedTo { get; init; }
    public DateTimeOffset? AssignedToLastUpdate { get; init; }

    public string? CreatedBy { get; init; }
    public DateTimeOffset? CreatedByLastUpdate { get; init; }

    public required DateTimeOffset LastUpdate { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
