using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

public interface IMergeService
{
    Task<int> FindAndResolveDuplicatesAsync(bool dryRun = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(Issue, Issue)>> CompareFilesAsync(string file1Path, string file2Path, CancellationToken cancellationToken = default);
}
