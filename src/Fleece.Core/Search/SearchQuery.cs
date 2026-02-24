namespace Fleece.Core.Search;

/// <summary>
/// A parsed search query containing zero or more search tokens.
/// </summary>
public sealed record SearchQuery
{
    /// <summary>
    /// The tokens comprising this query.
    /// </summary>
    public required IReadOnlyList<SearchToken> Tokens { get; init; }

    /// <summary>
    /// Whether this query is empty (no tokens).
    /// </summary>
    public bool IsEmpty => Tokens.Count == 0;

    /// <summary>
    /// An empty search query.
    /// </summary>
    public static SearchQuery Empty { get; } = new() { Tokens = [] };
}
