namespace Fleece.Core.Search;

/// <summary>
/// A single token in a search query.
/// </summary>
public sealed record SearchToken
{
    /// <summary>
    /// The type of this token.
    /// </summary>
    public required SearchTokenType Type { get; init; }

    /// <summary>
    /// Whether this filter is negated (excludes matches).
    /// </summary>
    public required bool IsNegated { get; init; }

    /// <summary>
    /// The values for this token.
    /// For text tokens, this contains the search text.
    /// For field filters, this contains the filter values (OR logic between values).
    /// </summary>
    public required IReadOnlyList<string> Values { get; init; }
}
