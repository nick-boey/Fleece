namespace Fleece.Core.Models;

public sealed record ConflictRecord
{
    public required Guid ConflictId { get; init; }
    public required string IssueId { get; init; }
    public required Issue OlderVersion { get; init; }
    public required Issue NewerVersion { get; init; }
    public required DateTimeOffset DetectedAt { get; init; }
}
