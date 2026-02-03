using Fleece.Core.Utilities;

namespace Fleece.Core.Models;

/// <summary>
/// Represents a reference to a parent issue with a sort order for maintaining position within the parent's children.
/// </summary>
public sealed record ParentIssueRef
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
    /// Parses a single parent issue reference from a string.
    /// Format: "issueId" or "issueId:sortOrder"
    /// </summary>
    /// <param name="input">The input string to parse.</param>
    /// <param name="defaultSortOrder">The default sort order to use if not specified in the input.</param>
    /// <returns>A new ParentIssueRef instance.</returns>
    public static ParentIssueRef ParseFromString(string input, string? defaultSortOrder)
    {
        var parts = input.Split(':', 2);
        var parentIssue = parts[0].Trim();
        var sortOrder = parts.Length > 1 ? parts[1].Trim() : defaultSortOrder ?? "aaa";

        return new ParentIssueRef
        {
            ParentIssue = parentIssue,
            SortOrder = sortOrder
        };
    }

    /// <summary>
    /// Parses a comma-separated list of parent issue references.
    /// Format: "issueId1,issueId2:sortOrder,issueId3"
    /// </summary>
    /// <param name="input">The comma-separated input string to parse.</param>
    /// <returns>A list of ParentIssueRef instances with generated sort orders for items without explicit ones.</returns>
    public static IReadOnlyList<ParentIssueRef> ParseFromStrings(string? input)
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

        // First pass: parse all items and collect those without explicit sort orders
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

        // Generate sort orders for items without them
        var generatedRanks = LexoRank.GenerateInitialRanks(indicesNeedingSortOrder.Count);

        var result = new List<ParentIssueRef>(items.Length);
        var rankIndex = 0;

        foreach (var (parentIssue, sortOrder, _) in parsed)
        {
            var effectiveSortOrder = sortOrder ?? generatedRanks[rankIndex++];
            result.Add(new ParentIssueRef
            {
                ParentIssue = parentIssue,
                SortOrder = effectiveSortOrder
            });
        }

        return result;
    }
}
