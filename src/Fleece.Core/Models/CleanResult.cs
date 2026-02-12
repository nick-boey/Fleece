namespace Fleece.Core.Models;

public sealed record CleanResult
{
    public required IReadOnlyList<Tombstone> CleanedTombstones { get; init; }
    public required IReadOnlyList<StrippedReference> StrippedReferences { get; init; }
}

public sealed record StrippedReference
{
    public required string IssueId { get; init; }
    public required string ReferencingIssueId { get; init; }
    public required string ReferenceType { get; init; }
}
