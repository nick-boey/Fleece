namespace Fleece.Core.Search;

/// <summary>
/// The type of a search token.
/// </summary>
public enum SearchTokenType
{
    /// <summary>
    /// Free-text search (matches title, description, tags).
    /// </summary>
    Text,

    /// <summary>
    /// Filter by issue status.
    /// </summary>
    StatusFilter,

    /// <summary>
    /// Filter by issue type.
    /// </summary>
    TypeFilter,

    /// <summary>
    /// Filter by priority.
    /// </summary>
    PriorityFilter,

    /// <summary>
    /// Filter by assigned user.
    /// </summary>
    AssignedFilter,

    /// <summary>
    /// Filter by tag.
    /// </summary>
    TagFilter,

    /// <summary>
    /// Filter by linked PR number.
    /// </summary>
    LinkedPrFilter,

    /// <summary>
    /// Filter by issue ID.
    /// </summary>
    IdFilter
}
