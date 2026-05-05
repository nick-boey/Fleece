namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Git information the event-sourced storage layer needs:
/// the current HEAD SHA (used as the replay-cache key), whether a given change
/// file is committed at HEAD (used to split committed vs uncommitted replay), and
/// commit-order tiebreaks (consumed by <see cref="IReplayEngine"/>).
/// </summary>
/// <remarks>
/// PR 1 ships <see cref="NullEventGitContext"/>; the CLI will plug in a real
/// git-backed implementation in PR 2 when the storage service is wired into DI.
/// </remarks>
public interface IEventGitContext : IChangeFileCommitOrder
{
    /// <summary>HEAD commit SHA, or <c>null</c> if not in a git repo / unborn HEAD.</summary>
    string? GetHeadSha();

    /// <summary>
    /// True if <paramref name="filePath"/> is tracked and present at HEAD.
    /// False for uncommitted, staged-only, or untracked files.
    /// </summary>
    bool IsFileCommittedAtHead(string filePath);
}

/// <summary>
/// Pessimistic default: not in a git repo, every file is uncommitted, no commit ordering.
/// Disables the replay cache (no HEAD SHA → no key).
/// </summary>
public sealed class NullEventGitContext : IEventGitContext
{
    public static readonly NullEventGitContext Instance = new();

    public string? GetHeadSha() => null;

    public bool IsFileCommittedAtHead(string filePath) => false;

    public int? GetFirstCommitOrdinal(string filePath) => null;
}
