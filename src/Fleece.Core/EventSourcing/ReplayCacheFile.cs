using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing;

/// <summary>
/// Shape of <c>.fleece/.replay-cache</c> — gitignored cache of the in-memory state
/// after replaying snapshot + all <em>committed</em> change files at the recorded
/// <see cref="HeadSha"/>. The active uncommitted change file is replayed on top
/// of this snapshot at read time.
/// </summary>
public sealed record ReplayCacheFile
{
    public required string HeadSha { get; init; }

    public required IReadOnlyList<Issue> Issues { get; init; }
}
