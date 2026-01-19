namespace Fleece.Core.Models;

public sealed record ConflictRecord
{
    public required Guid ConflictId { get; init; }
    public required string IssueId { get; init; }
    public required Issue OlderVersion { get; init; }
    public required Issue NewerVersion { get; init; }
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// Property-level conflict resolutions when merging. Null for legacy conflict records.
    /// </summary>
    public IReadOnlyList<PropertyConflict>? PropertyConflicts { get; init; }

    /// <summary>
    /// The merged issue result when property-level merging was used. Null for legacy conflict records.
    /// </summary>
    public Issue? MergedResult { get; init; }
}
