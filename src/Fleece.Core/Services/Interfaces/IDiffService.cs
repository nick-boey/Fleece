using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

public interface IDiffService
{
    /// <summary>
    /// Compares issues between two JSONL files and returns the differences.
    /// </summary>
    /// <param name="file1Path">Path to the first JSONL file.</param>
    /// <param name="file2Path">Path to the second JSONL file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A DiffResult containing modified issues and issues unique to each file.</returns>
    Task<DiffResult> CompareFilesAsync(
        string file1Path,
        string file2Path,
        CancellationToken cancellationToken = default);
}
