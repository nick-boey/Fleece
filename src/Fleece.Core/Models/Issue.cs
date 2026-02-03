namespace Fleece.Core.Models;

public sealed record Issue
{
    public required string Id { get; init; }

    public required string Title { get; init; }
    public DateTimeOffset TitleLastUpdate { get; init; }
    public string? TitleModifiedBy { get; init; }

    public string? Description { get; init; }
    public DateTimeOffset? DescriptionLastUpdate { get; init; }
    public string? DescriptionModifiedBy { get; init; }

    public required IssueStatus Status { get; init; }
    public DateTimeOffset StatusLastUpdate { get; init; }
    public string? StatusModifiedBy { get; init; }

    public required IssueType Type { get; init; }
    public DateTimeOffset TypeLastUpdate { get; init; }
    public string? TypeModifiedBy { get; init; }

    public int? LinkedPR { get; init; }
    public DateTimeOffset? LinkedPRLastUpdate { get; init; }
    public string? LinkedPRModifiedBy { get; init; }

    public IReadOnlyList<string> LinkedIssues { get; init; } = [];
    public DateTimeOffset LinkedIssuesLastUpdate { get; init; }
    public string? LinkedIssuesModifiedBy { get; init; }

    public IReadOnlyList<ParentIssueRef> ParentIssues { get; init; } = [];
    public DateTimeOffset ParentIssuesLastUpdate { get; init; }
    public string? ParentIssuesModifiedBy { get; init; }

    public int? Priority { get; init; }
    public DateTimeOffset? PriorityLastUpdate { get; init; }
    public string? PriorityModifiedBy { get; init; }

    public string? AssignedTo { get; init; }
    public DateTimeOffset? AssignedToLastUpdate { get; init; }
    public string? AssignedToModifiedBy { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
    public DateTimeOffset TagsLastUpdate { get; init; }
    public string? TagsModifiedBy { get; init; }

    public IReadOnlyList<Question> Questions { get; init; } = [];
    public DateTimeOffset QuestionsLastUpdate { get; init; }
    public string? QuestionsModifiedBy { get; init; }

    public string? WorkingBranchId { get; init; }
    public DateTimeOffset? WorkingBranchIdLastUpdate { get; init; }
    public string? WorkingBranchIdModifiedBy { get; init; }

    public string? CreatedBy { get; init; }
    public DateTimeOffset? CreatedByLastUpdate { get; init; }

    public required DateTimeOffset LastUpdate { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
