using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

public interface IMigrationService
{
    /// <summary>
    /// Migrates existing issues to have property timestamps.
    /// Issues without property timestamps will have them set based on LastUpdate.
    /// </summary>
    Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if migration is needed (issues exist without property timestamps).
    /// </summary>
    Task<bool> IsMigrationNeededAsync(CancellationToken cancellationToken = default);
}
