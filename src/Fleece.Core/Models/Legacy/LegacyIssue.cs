using System.Text.Json.Serialization;

namespace Fleece.Core.Models.Legacy;

/// <summary>
/// Legacy issue record carrying per-property <c>*LastUpdate</c>/<c>*ModifiedBy</c> metadata.
/// Used only by the legacy <c>IssueMerger</c> / <c>Merging</c> / <c>Migration</c> code paths.
/// New event-sourced storage uses <see cref="Issue"/> (the lean shape).
/// </summary>
public sealed record LegacyIssue
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

    public IReadOnlyList<LegacyParentIssueRef> ParentIssues { get; init; } = [];

    /// <summary>
    /// Returns only active parent issue references (excludes soft-deleted parents).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<LegacyParentIssueRef> ActiveParentIssues =>
        ParentIssues.Where(p => p.Active).ToList();

    public int? Priority { get; init; }
    public DateTimeOffset? PriorityLastUpdate { get; init; }
    public string? PriorityModifiedBy { get; init; }

    public string? AssignedTo { get; init; }
    public DateTimeOffset? AssignedToLastUpdate { get; init; }
    public string? AssignedToModifiedBy { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
    public DateTimeOffset TagsLastUpdate { get; init; }
    public string? TagsModifiedBy { get; init; }

    public string? WorkingBranchId { get; init; }
    public DateTimeOffset? WorkingBranchIdLastUpdate { get; init; }
    public string? WorkingBranchIdModifiedBy { get; init; }

    public ExecutionMode ExecutionMode { get; init; } = ExecutionMode.Series;
    public DateTimeOffset? ExecutionModeLastUpdate { get; init; }
    public string? ExecutionModeModifiedBy { get; init; }

    public string? CreatedBy { get; init; }
    public DateTimeOffset? CreatedByLastUpdate { get; init; }

    public required DateTimeOffset LastUpdate { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the linked PR numbers from keyed tags.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<int> LinkedPRs => KeyedTag.GetValues(Tags, KeyedTag.LinkedPrKey)
        .Select(v => int.TryParse(v, out var pr) ? pr : (int?)null)
        .Where(pr => pr.HasValue)
        .Select(pr => pr!.Value)
        .ToList();
}
