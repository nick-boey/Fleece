namespace Fleece.Core.Models;

/// <summary>
/// Lightweight summary of an issue used for sibling/child references in hierarchy output.
/// </summary>
public sealed record IssueSummaryDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required IssueStatus Status { get; init; }
    public required IssueType Type { get; init; }
    public int? Priority { get; init; }

    public static IssueSummaryDto FromIssue(Issue issue) => new()
    {
        Id = issue.Id,
        Title = issue.Title,
        Status = issue.Status,
        Type = issue.Type,
        Priority = issue.Priority
    };
}
