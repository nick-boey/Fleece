namespace Fleece.Core.Services.Interfaces;

public interface IMergeService
{
    Task<int> FindAndResolveDuplicatesAsync(bool dryRun = false, CancellationToken cancellationToken = default);
}
