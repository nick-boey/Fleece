namespace Fleece.Core.EventSourcing;

/// <summary>
/// Projected parent-issue reference. <c>parentIssue</c> is the natural key —
/// <c>remove</c> events match by ID only, ignoring <c>lexOrder</c>/<c>active</c>.
/// </summary>
public sealed record ParentIssueRef
{
    public required string ParentIssue { get; init; }

    public required string LexOrder { get; init; }

    public bool Active { get; init; } = true;
}
