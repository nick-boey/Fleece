using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing;

/// <summary>
/// Projected (lean) issue record persisted in <c>.fleece/issues.jsonl</c>.
/// Carries no per-property <c>*LastUpdate</c>/<c>*ModifiedBy</c> metadata —
/// that history lives in events under <c>.fleece/changes/</c>.
/// </summary>
public sealed record Issue
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

    public string? WorkingBranchId { get; init; }

    public ExecutionMode ExecutionMode { get; init; } = ExecutionMode.Series;

    public string? CreatedBy { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastUpdate { get; init; }
}
