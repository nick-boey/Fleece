namespace Fleece.Core.Models;

/// <summary>
/// Represents the git sync status of an issue.
/// </summary>
public enum SyncStatus
{
    /// <summary>
    /// Issue content is identical in the remote repository.
    /// </summary>
    Synced,

    /// <summary>
    /// Issue content is committed locally but not present or different in remote.
    /// </summary>
    Committed,

    /// <summary>
    /// Issue has local changes (staged or unstaged) not yet committed.
    /// </summary>
    Local
}
