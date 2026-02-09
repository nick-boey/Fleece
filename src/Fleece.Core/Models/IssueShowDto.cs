namespace Fleece.Core.Models;

/// <summary>
/// Enriched issue output for the show command, including hierarchy context.
/// This is a post-processing DTO and does not modify the persisted Issue model.
/// </summary>
public sealed record IssueShowDto
{
    public required IssueDto Issue { get; init; }
    public IReadOnlyList<ParentContextDto> Parents { get; init; } = [];
    public IReadOnlyList<IssueSummaryDto> Children { get; init; } = [];
    public ExecutionMode ExecutionMode { get; init; } = ExecutionMode.Series;
}
