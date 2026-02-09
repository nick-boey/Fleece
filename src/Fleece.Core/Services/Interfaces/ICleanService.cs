using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

public interface ICleanService
{
    Task<CleanResult> CleanAsync(
        bool includeComplete = false,
        bool includeClosed = false,
        bool includeArchived = false,
        bool stripReferences = true,
        bool dryRun = false,
        CancellationToken cancellationToken = default);
}
