namespace Fleece.Core.Models;

public sealed record Tombstone
{
    public required string IssueId { get; init; }
    public required string OriginalTitle { get; init; }
    public required DateTimeOffset CleanedAt { get; init; }
    public required string CleanedBy { get; init; }
}
