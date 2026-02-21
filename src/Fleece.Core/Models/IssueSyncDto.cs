namespace Fleece.Core.Models;

/// <summary>
/// A representation of an issue with sync status for JSON output.
/// </summary>
public sealed record IssueSyncDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required IssueStatus Status { get; init; }
    public required IssueType Type { get; init; }
    public int? LinkedPR { get; init; }
    public IReadOnlyList<string> LinkedIssues { get; init; } = [];
    public IReadOnlyList<ParentIssueRef> ParentIssues { get; init; } = [];
    public int? Priority { get; init; }
    public string? AssignedTo { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<Question> Questions { get; init; } = [];
    public string? CreatedBy { get; init; }
    public string? WorkingBranchId { get; init; }
    public ExecutionMode ExecutionMode { get; init; } = ExecutionMode.Series;
    public required DateTimeOffset LastUpdate { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public required SyncStatus SyncStatus { get; init; }

    public static IssueSyncDto FromIssue(Issue issue, SyncStatus syncStatus) => new()
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
        AssignedTo = issue.AssignedTo,
        Tags = issue.Tags,
        Questions = issue.Questions,
        CreatedBy = issue.CreatedBy,
        WorkingBranchId = issue.WorkingBranchId,
        ExecutionMode = issue.ExecutionMode,
        LastUpdate = issue.LastUpdate,
        CreatedAt = issue.CreatedAt,
        SyncStatus = syncStatus
    };
}
