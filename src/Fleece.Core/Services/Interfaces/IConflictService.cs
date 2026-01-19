using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

public interface IConflictService
{
    Task<IReadOnlyList<ConflictRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConflictRecord>> GetByIssueIdAsync(string issueId, CancellationToken cancellationToken = default);
    Task AddAsync(ConflictRecord conflict, CancellationToken cancellationToken = default);
    Task<bool> ClearByIssueIdAsync(string issueId, CancellationToken cancellationToken = default);
}
