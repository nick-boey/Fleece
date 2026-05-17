namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Git information the event-sourced storage layer needs:
/// the current HEAD SHA (used as the replay-cache key), whether a given change
/// file is committed at HEAD (load-bearing for the per-commit rotation rule in
/// <see cref="EventStore"/>; also splits committed vs uncommitted replay), and
/// commit-order tiebreaks (consumed by <see cref="IReplayEngine"/>).
/// </summary>
/// <remarks>
/// When the working tree is not a git repository, <see cref="NullEventGitContext"/>
/// is used; <see cref="IsFileCommittedAtHead"/> always returns false there, so
/// per-commit rotation degrades to per-session rotation. Non-git consumers have
/// no commits to rotate against, so that degradation is intentional.
/// </remarks>
public interface IEventGitContext : IChangeFileCommitOrder
{
    /// <summary>HEAD commit SHA, or <c>null</c> if not in a git repo / unborn HEAD.</summary>
    string? GetHeadSha();

    /// <summary>
    /// True if <paramref name="filePath"/> is tracked and present at HEAD.
    /// False for uncommitted, staged-only, or untracked files. Drives the
    /// "rotate when the active file is committed" trigger in <see cref="EventStore"/>.
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
