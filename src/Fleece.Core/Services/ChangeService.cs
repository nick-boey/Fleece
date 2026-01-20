using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class ChangeService : IChangeService
{
    private readonly IStorageService _storageService;

    public ChangeService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<IReadOnlyList<ChangeRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _storageService.LoadChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChangeRecord>> GetByIssueIdAsync(string issueId, CancellationToken cancellationToken = default)
    {
        var allChanges = await _storageService.LoadChangesAsync(cancellationToken);
        return allChanges
            .Where(c => c.IssueId.Equals(issueId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.ChangedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<ChangeRecord>> GetByUserAsync(string userName, CancellationToken cancellationToken = default)
    {
        var allChanges = await _storageService.LoadChangesAsync(cancellationToken);
        return allChanges
            .Where(c => c.ChangedBy.Equals(userName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.ChangedAt)
            .ToList();
    }

    public async Task AddAsync(ChangeRecord change, CancellationToken cancellationToken = default)
    {
        await _storageService.AppendChangeAsync(change, cancellationToken);
    }
}
