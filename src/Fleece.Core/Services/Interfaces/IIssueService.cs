using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

public interface IIssueService
{
    Task<Issue> CreateAsync(
        string title,
        IssueType type,
        string? description = null,
        IssueStatus status = IssueStatus.Idea,
        int? priority = null,
        int? linkedPr = null,
        IReadOnlyList<string>? linkedIssues = null,
        IReadOnlyList<string>? parentIssues = null,
        IReadOnlyList<string>? previousIssues = null,
        string? group = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        string? workingBranchId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Issue>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Issue?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<Issue> UpdateAsync(
        string id,
        string? title = null,
        string? description = null,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        int? linkedPr = null,
        IReadOnlyList<string>? linkedIssues = null,
        IReadOnlyList<string>? parentIssues = null,
        IReadOnlyList<string>? previousIssues = null,
        string? group = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        string? workingBranchId = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Issue>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Issue>> FilterAsync(
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? group = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        CancellationToken cancellationToken = default);

    Task<Issue> UpdateQuestionsAsync(
        string id,
        IReadOnlyList<Question> questions,
        CancellationToken cancellationToken = default);
}
