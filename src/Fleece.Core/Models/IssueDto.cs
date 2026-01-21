namespace Fleece.Core.Models;

/// <summary>
/// A simplified representation of an issue for JSON output without verbose metadata.
/// </summary>
public sealed record IssueDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required IssueStatus Status { get; init; }
    public required IssueType Type { get; init; }
    public int? LinkedPR { get; init; }
    public IReadOnlyList<string> LinkedIssues { get; init; } = [];
    public IReadOnlyList<string> ParentIssues { get; init; } = [];
    public int? Priority { get; init; }
    public string? Group { get; init; }
    public string? AssignedTo { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? CreatedBy { get; init; }
    public required DateTimeOffset LastUpdate { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public static IssueDto FromIssue(Issue issue) => new()
    {
        Id = issue.Id,
        Title = issue.Title,
        Description = issue.Description,
        Status = issue.Status,
        Type = issue.Type,
        LinkedPR = issue.LinkedPR,
        LinkedIssues = issue.LinkedIssues,
        ParentIssues = issue.ParentIssues,
        Priority = issue.Priority,
        Group = issue.Group,
        AssignedTo = issue.AssignedTo,
        Tags = issue.Tags,
        CreatedBy = issue.CreatedBy,
        LastUpdate = issue.LastUpdate,
        CreatedAt = issue.CreatedAt
    };
}
