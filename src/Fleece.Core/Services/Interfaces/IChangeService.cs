using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

public interface IChangeService
{
    Task<IReadOnlyList<ChangeRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChangeRecord>> GetByIssueIdAsync(string issueId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChangeRecord>> GetByUserAsync(string userName, CancellationToken cancellationToken = default);
    Task AddAsync(ChangeRecord change, CancellationToken cancellationToken = default);
}
