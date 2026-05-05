using Fleece.Core.Utilities;

namespace Fleece.Core.Models.Legacy;

/// <summary>
/// Legacy parent-issue reference carrying per-ref <c>LastUpdated</c>/<c>UpdatedBy</c> metadata.
/// Used only by the legacy <c>IssueMerger</c> / <c>Merging</c> / <c>Migration</c> code paths.
/// </summary>
public sealed record LegacyParentIssueRef
{
    /// <summary>
    /// The ID of the parent issue.
    /// </summary>
    public required string ParentIssue { get; init; }

    /// <summary>
    /// The lexicographic sort order for this issue within the parent's children.
    /// </summary>
    public required string SortOrder { get; init; }

    /// <summary>
    /// Timestamp when this parent reference was last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; init; }

    /// <summary>
    /// Username who last updated this parent reference.
    /// </summary>
    public string? UpdatedBy { get; init; }

    /// <summary>
    /// Whether this parent reference is active. Inactive references represent soft-deleted parent relationships.
    /// </summary>
    public bool Active { get; init; } = true;

    /// <summary>
    /// Parses a single parent issue reference from a string.
    /// Format: "issueId" or "issueId:sortOrder"
    /// </summary>
    public static LegacyParentIssueRef ParseFromString(string input, string? defaultSortOrder)
    {
        var parts = input.Split(':', 2);
        var parentIssue = parts[0].Trim();
        var sortOrder = parts.Length > 1 ? parts[1].Trim() : defaultSortOrder ?? "aaa";

        return new LegacyParentIssueRef
        {
            ParentIssue = parentIssue,
            SortOrder = sortOrder
        };
    }

    /// <summary>
    /// Parses a comma-separated list of parent issue references.
    /// Format: "issueId1,issueId2:sortOrder,issueId3"
    /// </summary>
    public static IReadOnlyList<LegacyParentIssueRef> ParseFromStrings(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var items = input.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (items.Length == 0)
        {
            return [];
        }

        var parsed = new List<(string ParentIssue, string? SortOrder, int Index)>();
        var indicesNeedingSortOrder = new List<int>();

        for (var i = 0; i < items.Length; i++)
        {
            var parts = items[i].Split(':', 2);
            var parentIssue = parts[0].Trim();
            var sortOrder = parts.Length > 1 ? parts[1].Trim() : null;

            parsed.Add((parentIssue, sortOrder, i));

            if (sortOrder is null)
            {
                indicesNeedingSortOrder.Add(i);
            }
        }

        var generatedRanks = LexoRank.GenerateInitialRanks(indicesNeedingSortOrder.Count);

        var result = new List<LegacyParentIssueRef>(items.Length);
        var rankIndex = 0;

        foreach (var (parentIssue, sortOrder, _) in parsed)
        {
            var effectiveSortOrder = sortOrder ?? generatedRanks[rankIndex++];
            result.Add(new LegacyParentIssueRef
            {
                ParentIssue = parentIssue,
                SortOrder = effectiveSortOrder
            });
        }

        return result;
    }
}
