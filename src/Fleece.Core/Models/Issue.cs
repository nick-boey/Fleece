namespace Fleece.Core.Models;

public sealed record Issue
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
    public required DateTimeOffset LastUpdate { get; init; }
}
