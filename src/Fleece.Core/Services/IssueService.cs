using System.Text.RegularExpressions;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed partial class IssueService(
    IStorageService storage,
    IIdGenerator idGenerator,
    IGitConfigService gitConfigService,
    IChangeService changeService) : IIssueService
{
    // Regex pattern for invalid Git branch name characters
    // Valid: alphanumeric, hyphen, underscore, forward slash (not at start/end), dot (not at start, not consecutive)
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9_\-/.]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$")]
    private static partial Regex ValidBranchNamePattern();

    [GeneratedRegex(@"[~^:\?\*\[\]@{}\s\\]|\.\.|\.$|^\.|\.lock$|//$|^/|/$")]
    private static partial Regex InvalidBranchNamePattern();

    /// <summary>
    /// Validates that a string is a valid Git branch name.
    /// </summary>
    internal static bool IsValidGitBranchName(string? branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return true; // null/empty is valid (means no branch)
        }

        // Check for invalid characters and patterns
        if (InvalidBranchNamePattern().IsMatch(branchName))
        {
            return false;
        }

        // Check for valid pattern
        return ValidBranchNamePattern().IsMatch(branchName);
    }

    public async Task<Issue> CreateAsync(
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
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (!IsValidGitBranchName(workingBranchId))
        {
            throw new ArgumentException("Working branch ID contains invalid characters for a Git branch name.", nameof(workingBranchId));
        }

        var id = idGenerator.Generate(title);
        var now = DateTimeOffset.UtcNow;
        var createdBy = gitConfigService.GetUserName();
        var issue = new Issue
        {
            Id = id,
            Title = title,
            TitleLastUpdate = now,
            TitleModifiedBy = createdBy,
            Description = description,
            DescriptionLastUpdate = description is not null ? now : null,
            DescriptionModifiedBy = description is not null ? createdBy : null,
            Status = status,
            StatusLastUpdate = now,
            StatusModifiedBy = createdBy,
            Type = type,
            TypeLastUpdate = now,
            TypeModifiedBy = createdBy,
            Priority = priority,
            PriorityLastUpdate = priority is not null ? now : null,
            PriorityModifiedBy = priority is not null ? createdBy : null,
            LinkedPR = linkedPr,
            LinkedPRLastUpdate = linkedPr is not null ? now : null,
            LinkedPRModifiedBy = linkedPr is not null ? createdBy : null,
            LinkedIssues = linkedIssues ?? [],
            LinkedIssuesLastUpdate = now,
            LinkedIssuesModifiedBy = createdBy,
            ParentIssues = parentIssues ?? [],
            ParentIssuesLastUpdate = now,
            ParentIssuesModifiedBy = createdBy,
            PreviousIssues = previousIssues ?? [],
            PreviousIssuesLastUpdate = now,
            PreviousIssuesModifiedBy = createdBy,
            Group = group,
            GroupLastUpdate = group is not null ? now : null,
            GroupModifiedBy = group is not null ? createdBy : null,
            AssignedTo = assignedTo,
            AssignedToLastUpdate = assignedTo is not null ? now : null,
            AssignedToModifiedBy = assignedTo is not null ? createdBy : null,
            Tags = tags ?? [],
            TagsLastUpdate = now,
            TagsModifiedBy = createdBy,
            WorkingBranchId = workingBranchId,
            WorkingBranchIdLastUpdate = workingBranchId is not null ? now : null,
            WorkingBranchIdModifiedBy = workingBranchId is not null ? createdBy : null,
            CreatedBy = createdBy,
            CreatedByLastUpdate = createdBy is not null ? now : null,
            LastUpdate = now,
            CreatedAt = now
        };

        await storage.AppendIssueAsync(issue, cancellationToken);

        // Record creation change
        var propertyChanges = new List<PropertyChange>
        {
            new() { PropertyName = "Title", OldValue = null, NewValue = title, Timestamp = now },
            new() { PropertyName = "Status", OldValue = null, NewValue = status.ToString(), Timestamp = now },
            new() { PropertyName = "Type", OldValue = null, NewValue = type.ToString(), Timestamp = now }
        };

        if (description is not null)
        {
            propertyChanges.Add(new() { PropertyName = "Description", OldValue = null, NewValue = description, Timestamp = now });
        }

        if (priority is not null)
        {
            propertyChanges.Add(new() { PropertyName = "Priority", OldValue = null, NewValue = priority.ToString(), Timestamp = now });
        }

        if (linkedPr is not null)
        {
            propertyChanges.Add(new() { PropertyName = "LinkedPR", OldValue = null, NewValue = linkedPr.ToString(), Timestamp = now });
        }

        if (linkedIssues is not null && linkedIssues.Count > 0)
        {
            propertyChanges.Add(new() { PropertyName = "LinkedIssues", OldValue = null, NewValue = string.Join(",", linkedIssues), Timestamp = now });
        }

        if (parentIssues is not null && parentIssues.Count > 0)
        {
            propertyChanges.Add(new() { PropertyName = "ParentIssues", OldValue = null, NewValue = string.Join(",", parentIssues), Timestamp = now });
        }

        if (previousIssues is not null && previousIssues.Count > 0)
        {
            propertyChanges.Add(new() { PropertyName = "PreviousIssues", OldValue = null, NewValue = string.Join(",", previousIssues), Timestamp = now });
        }

        if (group is not null)
        {
            propertyChanges.Add(new() { PropertyName = "Group", OldValue = null, NewValue = group, Timestamp = now });
        }

        if (assignedTo is not null)
        {
            propertyChanges.Add(new() { PropertyName = "AssignedTo", OldValue = null, NewValue = assignedTo, Timestamp = now });
        }

        if (tags is not null && tags.Count > 0)
        {
            propertyChanges.Add(new() { PropertyName = "Tags", OldValue = null, NewValue = string.Join(",", tags), Timestamp = now });
        }

        if (workingBranchId is not null)
        {
            propertyChanges.Add(new() { PropertyName = "WorkingBranchId", OldValue = null, NewValue = workingBranchId, Timestamp = now });
        }

        var changeRecord = new ChangeRecord
        {
            ChangeId = Guid.NewGuid(),
            IssueId = id,
            Type = ChangeType.Created,
            ChangedBy = createdBy ?? "unknown",
            ChangedAt = now,
            PropertyChanges = propertyChanges
        };

        await changeService.AddAsync(changeRecord, cancellationToken);

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
        IReadOnlyList<string>? previousIssues = null,
        string? group = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        string? workingBranchId = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidGitBranchName(workingBranchId))
        {
            throw new ArgumentException("Working branch ID contains invalid characters for a Git branch name.", nameof(workingBranchId));
        }

        var issues = (await storage.LoadIssuesAsync(cancellationToken)).ToList();
        var existingIndex = issues.FindIndex(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (existingIndex < 0)
        {
            throw new KeyNotFoundException($"Issue with ID '{id}' not found.");
        }

        var existing = issues[existingIndex];
        var now = DateTimeOffset.UtcNow;
        var modifiedBy = gitConfigService.GetUserName();
        var newId = title is not null ? idGenerator.Generate(title) : existing.Id;

        var propertyChanges = new List<PropertyChange>();

        var updated = new Issue
        {
            Id = newId,
            Title = title ?? existing.Title,
            TitleLastUpdate = title is not null ? now : existing.TitleLastUpdate,
            TitleModifiedBy = title is not null ? modifiedBy : existing.TitleModifiedBy,
            Description = description ?? existing.Description,
            DescriptionLastUpdate = description is not null ? now : existing.DescriptionLastUpdate,
            DescriptionModifiedBy = description is not null ? modifiedBy : existing.DescriptionModifiedBy,
            Status = status ?? existing.Status,
            StatusLastUpdate = status is not null ? now : existing.StatusLastUpdate,
            StatusModifiedBy = status is not null ? modifiedBy : existing.StatusModifiedBy,
            Type = type ?? existing.Type,
            TypeLastUpdate = type is not null ? now : existing.TypeLastUpdate,
            TypeModifiedBy = type is not null ? modifiedBy : existing.TypeModifiedBy,
            Priority = priority ?? existing.Priority,
            PriorityLastUpdate = priority is not null ? now : existing.PriorityLastUpdate,
            PriorityModifiedBy = priority is not null ? modifiedBy : existing.PriorityModifiedBy,
            LinkedPR = linkedPr ?? existing.LinkedPR,
            LinkedPRLastUpdate = linkedPr is not null ? now : existing.LinkedPRLastUpdate,
            LinkedPRModifiedBy = linkedPr is not null ? modifiedBy : existing.LinkedPRModifiedBy,
            LinkedIssues = linkedIssues ?? existing.LinkedIssues,
            LinkedIssuesLastUpdate = linkedIssues is not null ? now : existing.LinkedIssuesLastUpdate,
            LinkedIssuesModifiedBy = linkedIssues is not null ? modifiedBy : existing.LinkedIssuesModifiedBy,
            ParentIssues = parentIssues ?? existing.ParentIssues,
            ParentIssuesLastUpdate = parentIssues is not null ? now : existing.ParentIssuesLastUpdate,
            ParentIssuesModifiedBy = parentIssues is not null ? modifiedBy : existing.ParentIssuesModifiedBy,
            PreviousIssues = previousIssues ?? existing.PreviousIssues,
            PreviousIssuesLastUpdate = previousIssues is not null ? now : existing.PreviousIssuesLastUpdate,
            PreviousIssuesModifiedBy = previousIssues is not null ? modifiedBy : existing.PreviousIssuesModifiedBy,
            Group = group ?? existing.Group,
            GroupLastUpdate = group is not null ? now : existing.GroupLastUpdate,
            GroupModifiedBy = group is not null ? modifiedBy : existing.GroupModifiedBy,
            AssignedTo = assignedTo ?? existing.AssignedTo,
            AssignedToLastUpdate = assignedTo is not null ? now : existing.AssignedToLastUpdate,
            AssignedToModifiedBy = assignedTo is not null ? modifiedBy : existing.AssignedToModifiedBy,
            Tags = tags ?? existing.Tags,
            TagsLastUpdate = tags is not null ? now : existing.TagsLastUpdate,
            TagsModifiedBy = tags is not null ? modifiedBy : existing.TagsModifiedBy,
            WorkingBranchId = workingBranchId ?? existing.WorkingBranchId,
            WorkingBranchIdLastUpdate = workingBranchId is not null ? now : existing.WorkingBranchIdLastUpdate,
            WorkingBranchIdModifiedBy = workingBranchId is not null ? modifiedBy : existing.WorkingBranchIdModifiedBy,
            CreatedBy = existing.CreatedBy,
            CreatedByLastUpdate = existing.CreatedByLastUpdate,
            LastUpdate = now,
            CreatedAt = existing.CreatedAt
        };

        // Record property changes
        if (title is not null)
        {
            propertyChanges.Add(new() { PropertyName = "Title", OldValue = existing.Title, NewValue = title, Timestamp = now });
        }

        if (description is not null)
        {
            propertyChanges.Add(new() { PropertyName = "Description", OldValue = existing.Description, NewValue = description, Timestamp = now });
        }

        if (status is not null)
        {
            propertyChanges.Add(new() { PropertyName = "Status", OldValue = existing.Status.ToString(), NewValue = status.ToString(), Timestamp = now });
        }

        if (type is not null)
        {
            propertyChanges.Add(new() { PropertyName = "Type", OldValue = existing.Type.ToString(), NewValue = type.ToString(), Timestamp = now });
        }

        if (priority is not null)
        {
            propertyChanges.Add(new() { PropertyName = "Priority", OldValue = existing.Priority?.ToString(), NewValue = priority.ToString(), Timestamp = now });
        }

        if (linkedPr is not null)
        {
            propertyChanges.Add(new() { PropertyName = "LinkedPR", OldValue = existing.LinkedPR?.ToString(), NewValue = linkedPr.ToString(), Timestamp = now });
        }

        if (linkedIssues is not null)
        {
            propertyChanges.Add(new() { PropertyName = "LinkedIssues", OldValue = string.Join(",", existing.LinkedIssues), NewValue = string.Join(",", linkedIssues), Timestamp = now });
        }

        if (parentIssues is not null)
        {
            propertyChanges.Add(new() { PropertyName = "ParentIssues", OldValue = string.Join(",", existing.ParentIssues), NewValue = string.Join(",", parentIssues), Timestamp = now });
        }

        if (previousIssues is not null)
        {
            propertyChanges.Add(new() { PropertyName = "PreviousIssues", OldValue = string.Join(",", existing.PreviousIssues), NewValue = string.Join(",", previousIssues), Timestamp = now });
        }

        if (group is not null)
        {
            propertyChanges.Add(new() { PropertyName = "Group", OldValue = existing.Group, NewValue = group, Timestamp = now });
        }

        if (assignedTo is not null)
        {
            propertyChanges.Add(new() { PropertyName = "AssignedTo", OldValue = existing.AssignedTo, NewValue = assignedTo, Timestamp = now });
        }

        if (tags is not null)
        {
            propertyChanges.Add(new() { PropertyName = "Tags", OldValue = string.Join(",", existing.Tags), NewValue = string.Join(",", tags), Timestamp = now });
        }

        if (workingBranchId is not null)
        {
            propertyChanges.Add(new() { PropertyName = "WorkingBranchId", OldValue = existing.WorkingBranchId, NewValue = workingBranchId, Timestamp = now });
        }

        issues[existingIndex] = updated;
        await storage.SaveIssuesAsync(issues, cancellationToken);

        // Record update change if there were any property changes
        if (propertyChanges.Count > 0)
        {
            var changeRecord = new ChangeRecord
            {
                ChangeId = Guid.NewGuid(),
                IssueId = newId,
                Type = ChangeType.Updated,
                ChangedBy = modifiedBy ?? "unknown",
                ChangedAt = now,
                PropertyChanges = propertyChanges
            };

            await changeService.AddAsync(changeRecord, cancellationToken);
        }

        return updated;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var issues = (await storage.LoadIssuesAsync(cancellationToken)).ToList();
        var existingIndex = issues.FindIndex(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (existingIndex < 0)
        {
            return false;
        }

        var existing = issues[existingIndex];
        var now = DateTimeOffset.UtcNow;
        var modifiedBy = gitConfigService.GetUserName();
        var deleted = existing with
        {
            Status = IssueStatus.Deleted,
            StatusLastUpdate = now,
            StatusModifiedBy = modifiedBy,
            LastUpdate = now
        };

        issues[existingIndex] = deleted;
        await storage.SaveIssuesAsync(issues, cancellationToken);

        // Record deletion change
        var changeRecord = new ChangeRecord
        {
            ChangeId = Guid.NewGuid(),
            IssueId = id,
            Type = ChangeType.Deleted,
            ChangedBy = modifiedBy ?? "unknown",
            ChangedAt = now,
            PropertyChanges =
            [
                new PropertyChange
                {
                    PropertyName = "Status",
                    OldValue = existing.Status.ToString(),
                    NewValue = IssueStatus.Deleted.ToString(),
                    Timestamp = now
                }
            ]
        };

        await changeService.AddAsync(changeRecord, cancellationToken);

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
                (i.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                i.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (i.Group?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
    }

    public async Task<IReadOnlyList<Issue>> FilterAsync(
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? group = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        CancellationToken cancellationToken = default)
    {
        var issues = await storage.LoadIssuesAsync(cancellationToken);

        return issues
            .Where(i => status is null || i.Status == status)
            .Where(i => type is null || i.Type == type)
            .Where(i => priority is null || i.Priority == priority)
            .Where(i => group is null || string.Equals(i.Group, group, StringComparison.OrdinalIgnoreCase))
            .Where(i => assignedTo is null || string.Equals(i.AssignedTo, assignedTo, StringComparison.OrdinalIgnoreCase))
            .Where(i => tags is null || tags.Count == 0 || tags.Any(t => i.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
            .Where(i => linkedPr is null || i.LinkedPR == linkedPr)
            .ToList();
    }

    public async Task<Issue> UpdateQuestionsAsync(
        string id,
        IReadOnlyList<Question> questions,
        CancellationToken cancellationToken = default)
    {
        var issues = (await storage.LoadIssuesAsync(cancellationToken)).ToList();
        var existingIndex = issues.FindIndex(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (existingIndex < 0)
        {
            throw new KeyNotFoundException($"Issue with ID '{id}' not found.");
        }

        var existing = issues[existingIndex];
        var now = DateTimeOffset.UtcNow;
        var modifiedBy = gitConfigService.GetUserName();

        var updated = existing with
        {
            Questions = questions,
            QuestionsLastUpdate = now,
            QuestionsModifiedBy = modifiedBy,
            LastUpdate = now
        };

        issues[existingIndex] = updated;
        await storage.SaveIssuesAsync(issues, cancellationToken);

        // Record change
        var changeRecord = new ChangeRecord
        {
            ChangeId = Guid.NewGuid(),
            IssueId = id,
            Type = ChangeType.Updated,
            ChangedBy = modifiedBy ?? "unknown",
            ChangedAt = now,
            PropertyChanges =
            [
                new PropertyChange
                {
                    PropertyName = "Questions",
                    OldValue = $"{(existing.Questions?.Count ?? 0)} question(s)",
                    NewValue = $"{questions.Count} question(s)",
                    Timestamp = now
                }
            ]
        };

        await changeService.AddAsync(changeRecord, cancellationToken);

        return updated;
    }
}
