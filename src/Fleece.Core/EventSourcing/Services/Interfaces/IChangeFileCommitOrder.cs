namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Provides the first-commit ordinal for a change file on the current branch.
/// Used by <see cref="IReplayEngine"/> to tiebreak between sibling DAG nodes when
/// neither <c>follows</c> ordering nor GUID alphabetical ordering is sufficient.
/// </summary>
/// <remarks>
/// The default implementation is <see cref="NullChangeFileCommitOrder"/>, which returns
/// <c>null</c> for every file — i.e., commit order does not contribute to ordering.
/// In production the CLI plugs in a git-backed implementation.
/// </remarks>
public interface IChangeFileCommitOrder
{
    /// <summary>
    /// Returns the ordinal of the first commit on the current branch that introduced
    /// <paramref name="filePath"/>. Smaller = earlier. <c>null</c> if the file is uncommitted
    /// or commit-order data is unavailable; such files sort last.
    /// </summary>
    int? GetFirstCommitOrdinal(string filePath);
}

/// <summary>No-op implementation: no commit-order tiebreaks. Every file gets <c>null</c>.</summary>
public sealed class NullChangeFileCommitOrder : IChangeFileCommitOrder
{
    public static readonly NullChangeFileCommitOrder Instance = new();
    public int? GetFirstCommitOrdinal(string filePath) => null;
}
