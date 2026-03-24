namespace Fleece.Core.Models;

/// <summary>
/// The outcome of a move up/down operation.
/// </summary>
public enum MoveOutcome
{
    /// <summary>The issue was moved up (before its previous sibling).</summary>
    MovedUp,

    /// <summary>The issue was moved down (after its next sibling).</summary>
    MovedDown,

    /// <summary>The move was invalid and no changes were made.</summary>
    Invalid
}

/// <summary>
/// The reason a move operation was invalid.
/// </summary>
public enum MoveInvalidReason
{
    /// <summary>The move was valid (no invalid reason).</summary>
    None,

    /// <summary>The issue is already at the top of the sibling list.</summary>
    AlreadyAtTop,

    /// <summary>The issue is already at the bottom of the sibling list.</summary>
    AlreadyAtBottom,

    /// <summary>The issue is not a child of the specified parent.</summary>
    NotAChildOfParent
}

/// <summary>
/// Result of a move up/down operation on an issue within its parent's children.
/// </summary>
public sealed record MoveResult
{
    /// <summary>
    /// The outcome of the move operation.
    /// </summary>
    public required MoveOutcome Outcome { get; init; }

    /// <summary>
    /// The reason the move was invalid, if applicable.
    /// </summary>
    public MoveInvalidReason Reason { get; init; } = MoveInvalidReason.None;

    /// <summary>
    /// The updated issue after the move, or null if the move was invalid.
    /// </summary>
    public Issue? UpdatedIssue { get; init; }

    /// <summary>
    /// A human-readable message describing the result.
    /// </summary>
    public string? Message { get; init; }
}
