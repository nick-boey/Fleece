using System.Text.Json.Serialization;
using Fleece.Core.Models.Graph;

namespace Fleece.Core.Models;

/// <summary>
/// Projected (lean) issue record persisted in <c>.fleece/issues.jsonl</c>.
/// Carries no per-property <c>*LastUpdate</c>/<c>*ModifiedBy</c> metadata —
/// that history lives in events under <c>.fleece/changes/</c>.
/// </summary>
public sealed record Issue : IGraphNode
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

    /// <summary>
    /// Returns only active parent issue references (excludes soft-deleted parents).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ParentIssueRef> ActiveParentIssues =>
        ParentIssues.Where(p => p.Active).ToList();

    [JsonIgnore]
    ChildSequencing IGraphNode.ChildSequencing =>
        ExecutionMode == ExecutionMode.Series ? ChildSequencing.Series : ChildSequencing.Parallel;

    /// <summary>
    /// Gets the linked PR numbers from keyed tags.
    /// This is the preferred way to access linked PRs.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<int> LinkedPRs => KeyedTag.GetValues(Tags, KeyedTag.LinkedPrKey)
        .Select(v => int.TryParse(v, out var pr) ? pr : (int?)null)
        .Where(pr => pr.HasValue)
        .Select(pr => pr!.Value)
        .ToList();
}
