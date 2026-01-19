using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

public interface IStorageService
{
    Task<IReadOnlyList<Issue>> LoadIssuesAsync(CancellationToken cancellationToken = default);
    Task SaveIssuesAsync(IReadOnlyList<Issue> issues, CancellationToken cancellationToken = default);
    Task AppendIssueAsync(Issue issue, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConflictRecord>> LoadConflictsAsync(CancellationToken cancellationToken = default);
    Task SaveConflictsAsync(IReadOnlyList<ConflictRecord> conflicts, CancellationToken cancellationToken = default);
    Task AppendConflictAsync(ConflictRecord conflict, CancellationToken cancellationToken = default);
    Task EnsureDirectoryExistsAsync(CancellationToken cancellationToken = default);
}
