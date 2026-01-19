using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class ConflictService(IStorageService storage) : IConflictService
{
    public async Task<IReadOnlyList<ConflictRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await storage.LoadConflictsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConflictRecord>> GetByIssueIdAsync(string issueId, CancellationToken cancellationToken = default)
    {
        var conflicts = await storage.LoadConflictsAsync(cancellationToken);
        return conflicts.Where(c => c.IssueId.Equals(issueId, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task AddAsync(ConflictRecord conflict, CancellationToken cancellationToken = default)
    {
        await storage.AppendConflictAsync(conflict, cancellationToken);
    }

    public async Task<bool> ClearByIssueIdAsync(string issueId, CancellationToken cancellationToken = default)
    {
        var conflicts = (await storage.LoadConflictsAsync(cancellationToken)).ToList();
        var removed = conflicts.RemoveAll(c => c.IssueId.Equals(issueId, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
        {
            return false;
        }

        await storage.SaveConflictsAsync(conflicts, cancellationToken);
        return true;
    }
}
