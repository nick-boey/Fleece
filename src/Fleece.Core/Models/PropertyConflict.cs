namespace Fleece.Core.Models;

/// <summary>
/// Represents a conflict resolution for a single property during merge.
/// </summary>
public sealed record PropertyConflict
{
    public required string PropertyName { get; init; }
    public required string? ValueA { get; init; }
    public required DateTimeOffset? TimestampA { get; init; }
    public required string? ValueB { get; init; }
    public required DateTimeOffset? TimestampB { get; init; }
    public required string? ResolvedValue { get; init; }
    public required string Resolution { get; init; } // "A", "B", or "Union"
}
