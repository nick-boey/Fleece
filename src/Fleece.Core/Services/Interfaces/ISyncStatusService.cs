using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Service for determining the git sync status of issues.
/// </summary>
public interface ISyncStatusService
{
    /// <summary>
    /// Gets the sync status for all issues by comparing working directory,
    /// HEAD commit, and remote upstream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping issue IDs to their sync status.</returns>
    Task<IReadOnlyDictionary<string, SyncStatus>> GetSyncStatusesAsync(
        CancellationToken cancellationToken = default);
}
