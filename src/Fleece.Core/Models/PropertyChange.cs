namespace Fleece.Core.Models;

/// <summary>
/// Represents a change to a single property.
/// </summary>
public sealed record PropertyChange
{
    public required string PropertyName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// For merge operations: "A", "B", or "Union". Null for non-merge changes.
    /// </summary>
    public string? MergeResolution { get; init; }
}
