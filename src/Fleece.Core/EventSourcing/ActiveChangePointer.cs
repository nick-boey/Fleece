namespace Fleece.Core.EventSourcing;

/// <summary>
/// Shape of <c>.fleece/.active-change</c> — a per-working-tree gitignored pointer
/// recording the GUID of the change file that the current session is appending to.
/// </summary>
public sealed record ActiveChangePointer
{
    public required string Guid { get; init; }
}
