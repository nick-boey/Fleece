using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class IssueService(IStorageService storage, IIdGenerator idGenerator) : IIssueService
{
    public async Task<Issue> CreateAsync(
        string title,
        IssueType type,
        string? description = null,
        IssueStatus status = IssueStatus.Open,
        int? priority = null,
        int? linkedPr = null,
        IReadOnlyList<string>? linkedIssues = null,
        IReadOnlyList<string>? parentIssues = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var id = idGenerator.Generate(title);
        var issue = new Issue
        {
            Id = id,
            Title = title,
            Description = description,
            Status = status,
            Type = type,
            Priority = priority,
            LinkedPR = linkedPr,
            LinkedIssues = linkedIssues ?? [],
            ParentIssues = parentIssues ?? [],
            LastUpdate = DateTimeOffset.UtcNow
        };

        await storage.AppendIssueAsync(issue, cancellationToken);
        return issue;
    }

    public async Task<IReadOnlyList<Issue>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await storage.LoadIssuesAsync(cancellationToken);
    }

    public async Task<Issue?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var issues = await storage.LoadIssuesAsync(cancellationToken);
        return issues.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Issue> UpdateAsync(
        string id,
        string? title = null,
        string? description = null,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        int? linkedPr = null,
        IReadOnlyList<string>? linkedIssues = null,
        IReadOnlyList<string>? parentIssues = null,
        CancellationToken cancellationToken = default)
    {
        var issues = (await storage.LoadIssuesAsync(cancellationToken)).ToList();
        var existingIndex = issues.FindIndex(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (existingIndex < 0)
        {
            throw new KeyNotFoundException($"Issue with ID '{id}' not found.");
        }

        var existing = issues[existingIndex];
        var newId = title is not null ? idGenerator.Generate(title) : existing.Id;

        var updated = new Issue
        {
            Id = newId,
            Title = title ?? existing.Title,
            Description = description ?? existing.Description,
            Status = status ?? existing.Status,
            Type = type ?? existing.Type,
            Priority = priority ?? existing.Priority,
            LinkedPR = linkedPr ?? existing.LinkedPR,
            LinkedIssues = linkedIssues ?? existing.LinkedIssues,
            ParentIssues = parentIssues ?? existing.ParentIssues,
            LastUpdate = DateTimeOffset.UtcNow
        };

        issues[existingIndex] = updated;
        await storage.SaveIssuesAsync(issues, cancellationToken);

        return updated;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var issues = (await storage.LoadIssuesAsync(cancellationToken)).ToList();
        var removed = issues.RemoveAll(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
        {
            return false;
        }

        await storage.SaveIssuesAsync(issues, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<Issue>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var issues = await storage.LoadIssuesAsync(cancellationToken);
        return issues
            .Where(i =>
                i.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (i.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
    }

    public async Task<IReadOnlyList<Issue>> FilterAsync(
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        CancellationToken cancellationToken = default)
    {
        var issues = await storage.LoadIssuesAsync(cancellationToken);

        return issues
            .Where(i => status is null || i.Status == status)
            .Where(i => type is null || i.Type == type)
            .Where(i => priority is null || i.Priority == priority)
            .ToList();
    }
}
