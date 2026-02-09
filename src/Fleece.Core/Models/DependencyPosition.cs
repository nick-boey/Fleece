namespace Fleece.Core.Models;

/// <summary>
/// Specifies the kind of sort order positioning for a dependency.
/// </summary>
public enum DependencyPositionKind
{
    /// <summary>Append at end (default).</summary>
    Last,

    /// <summary>Place at beginning.</summary>
    First,

    /// <summary>Place after a specific sibling.</summary>
    After,

    /// <summary>Place before a specific sibling.</summary>
    Before
}

/// <summary>
/// Represents the desired sort order position when adding a dependency.
/// </summary>
public record DependencyPosition
{
    /// <summary>
    /// The kind of positioning to apply.
    /// </summary>
    public DependencyPositionKind Kind { get; init; } = DependencyPositionKind.Last;

    /// <summary>
    /// The sibling issue ID for After/Before positioning (may be partial).
    /// </summary>
    public string? SiblingId { get; init; }
}
