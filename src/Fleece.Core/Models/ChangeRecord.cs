namespace Fleece.Core.Models;

/// <summary>
/// Records a change event for an issue.
/// </summary>
public sealed record ChangeRecord
{
    public required Guid ChangeId { get; init; }
    public required string IssueId { get; init; }
    public required ChangeType Type { get; init; }
    public required string ChangedBy { get; init; }
    public required DateTimeOffset ChangedAt { get; init; }
    public IReadOnlyList<PropertyChange> PropertyChanges { get; init; } = [];
}
