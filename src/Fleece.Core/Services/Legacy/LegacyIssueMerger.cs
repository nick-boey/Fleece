using Fleece.Core.Models;
using Fleece.Core.Models.Legacy;

namespace Fleece.Core.Services.Legacy;

/// <summary>
/// Legacy property-by-property merger for <see cref="LegacyIssue"/>. Replaced by
/// event-replay merging in event-sourced storage; retained only for historical reference
/// and any remaining legacy migration paths.
/// </summary>
public sealed class LegacyIssueMerger
{
    /// <summary>
    /// Merges two versions of the same legacy issue, keeping the newer value for each property.
    /// Collections (LinkedIssues, ParentIssues) use union strategy.
    /// </summary>
    public LegacyMergeResult Merge(LegacyIssue issueA, LegacyIssue issueB, string? mergedBy = null)
    {
        if (!issueA.Id.Equals(issueB.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Cannot merge issues with different IDs: '{issueA.Id}' and '{issueB.Id}'");
        }

        var changes = new List<PropertyChange>();
        var now = DateTimeOffset.UtcNow;

        // Merge Title
        var (title, titleTimestamp, titleModifiedBy, titleChange) = MergeProperty(
            "Title", issueA.Title, issueA.TitleLastUpdate, issueA.TitleModifiedBy,
            issueB.Title, issueB.TitleLastUpdate, issueB.TitleModifiedBy, mergedBy, now);
        if (titleChange is not null)
        {
            changes.Add(titleChange);
        }

        // Merge Description
        var (description, descriptionTimestamp, descriptionModifiedBy, descriptionChange) = MergeNullableProperty(
            "Description", issueA.Description, issueA.DescriptionLastUpdate, issueA.DescriptionModifiedBy,
            issueB.Description, issueB.DescriptionLastUpdate, issueB.DescriptionModifiedBy, mergedBy, now);
        if (descriptionChange is not null)
        {
            changes.Add(descriptionChange);
        }

        // Merge Status
        var (status, statusTimestamp, statusModifiedBy, statusChange) = MergeProperty(
            "Status", issueA.Status, issueA.StatusLastUpdate, issueA.StatusModifiedBy,
            issueB.Status, issueB.StatusLastUpdate, issueB.StatusModifiedBy, mergedBy, now);
        if (statusChange is not null)
        {
            changes.Add(statusChange);
        }

        // Merge Type
        var (type, typeTimestamp, typeModifiedBy, typeChange) = MergeProperty(
            "Type", issueA.Type, issueA.TypeLastUpdate, issueA.TypeModifiedBy,
            issueB.Type, issueB.TypeLastUpdate, issueB.TypeModifiedBy, mergedBy, now);
        if (typeChange is not null)
        {
            changes.Add(typeChange);
        }

        // Merge Priority
        var (priority, priorityTimestamp, priorityModifiedBy, priorityChange) = MergeNullableProperty(
            "Priority", issueA.Priority, issueA.PriorityLastUpdate, issueA.PriorityModifiedBy,
            issueB.Priority, issueB.PriorityLastUpdate, issueB.PriorityModifiedBy, mergedBy, now);
        if (priorityChange is not null)
        {
            changes.Add(priorityChange);
        }

        // Merge LinkedPR
        var (linkedPR, linkedPRTimestamp, linkedPRModifiedBy, linkedPRChange) = MergeNullableProperty(
            "LinkedPR", issueA.LinkedPR, issueA.LinkedPRLastUpdate, issueA.LinkedPRModifiedBy,
            issueB.LinkedPR, issueB.LinkedPRLastUpdate, issueB.LinkedPRModifiedBy, mergedBy, now);
        if (linkedPRChange is not null)
        {
            changes.Add(linkedPRChange);
        }

        // Merge collections with union strategy
        var (linkedIssues, linkedIssuesTimestamp, linkedIssuesModifiedBy, linkedIssuesChange) = MergeStringCollections(
            "LinkedIssues", issueA.LinkedIssues, issueA.LinkedIssuesLastUpdate, issueA.LinkedIssuesModifiedBy,
            issueB.LinkedIssues, issueB.LinkedIssuesLastUpdate, issueB.LinkedIssuesModifiedBy, mergedBy, now);
        if (linkedIssuesChange is not null)
        {
            changes.Add(linkedIssuesChange);
        }

        var (parentIssues, parentIssuesChange) = MergeParentIssueCollections(
            "ParentIssues", issueA.ParentIssues, issueB.ParentIssues, mergedBy, now);
        if (parentIssuesChange is not null)
        {
            changes.Add(parentIssuesChange);
        }

        // Merge AssignedTo
        var (assignedTo, assignedToTimestamp, assignedToModifiedBy, assignedToChange) = MergeNullableProperty(
            "AssignedTo", issueA.AssignedTo, issueA.AssignedToLastUpdate, issueA.AssignedToModifiedBy,
            issueB.AssignedTo, issueB.AssignedToLastUpdate, issueB.AssignedToModifiedBy, mergedBy, now);
        if (assignedToChange is not null)
        {
            changes.Add(assignedToChange);
        }

        // Merge Tags
        var (tags, tagsTimestamp, tagsModifiedBy, tagsChange) = MergeStringCollections(
            "Tags", issueA.Tags, issueA.TagsLastUpdate, issueA.TagsModifiedBy,
            issueB.Tags, issueB.TagsLastUpdate, issueB.TagsModifiedBy, mergedBy, now);
        if (tagsChange is not null)
        {
            changes.Add(tagsChange);
        }

        // Merge ExecutionMode
        var (executionMode, executionModeTimestamp, executionModeModifiedBy, executionModeChange) = MergeNullableProperty(
            "ExecutionMode", (ExecutionMode?)issueA.ExecutionMode, issueA.ExecutionModeLastUpdate, issueA.ExecutionModeModifiedBy,
            (ExecutionMode?)issueB.ExecutionMode, issueB.ExecutionModeLastUpdate, issueB.ExecutionModeModifiedBy, mergedBy, now);
        if (executionModeChange is not null)
        {
            changes.Add(executionModeChange);
        }

        // Merge WorkingBranchId
        var (workingBranchId, workingBranchIdTimestamp, workingBranchIdModifiedBy, workingBranchIdChange) = MergeNullableProperty(
            "WorkingBranchId", issueA.WorkingBranchId, issueA.WorkingBranchIdLastUpdate, issueA.WorkingBranchIdModifiedBy,
            issueB.WorkingBranchId, issueB.WorkingBranchIdLastUpdate, issueB.WorkingBranchIdModifiedBy, mergedBy, now);
        if (workingBranchIdChange is not null)
        {
            changes.Add(workingBranchIdChange);
        }

        // Merge CreatedBy - keep oldest non-null value (creator never changes)
        var (createdBy, createdByTimestamp) = MergeCreatedBy(
            issueA.CreatedBy, issueA.CreatedByLastUpdate, issueB.CreatedBy, issueB.CreatedByLastUpdate);

        // Use the older CreatedAt
        var createdAt = issueA.CreatedAt < issueB.CreatedAt ? issueA.CreatedAt : issueB.CreatedAt;
        if (issueA.CreatedAt == default)
        {
            createdAt = issueB.CreatedAt;
        }

        if (issueB.CreatedAt == default)
        {
            createdAt = issueA.CreatedAt;
        }

        // Use the newer LastUpdate
        var lastUpdate = issueA.LastUpdate > issueB.LastUpdate ? issueA.LastUpdate : issueB.LastUpdate;

        var mergedIssue = new LegacyIssue
        {
            Id = issueA.Id,
            Title = title,
            TitleLastUpdate = titleTimestamp,
            TitleModifiedBy = titleModifiedBy,
            Description = description,
            DescriptionLastUpdate = descriptionTimestamp,
            DescriptionModifiedBy = descriptionModifiedBy,
            Status = status,
            StatusLastUpdate = statusTimestamp,
            StatusModifiedBy = statusModifiedBy,
            Type = type,
            TypeLastUpdate = typeTimestamp,
            TypeModifiedBy = typeModifiedBy,
            Priority = priority,
            PriorityLastUpdate = priorityTimestamp,
            PriorityModifiedBy = priorityModifiedBy,
            LinkedPR = linkedPR,
            LinkedPRLastUpdate = linkedPRTimestamp,
            LinkedPRModifiedBy = linkedPRModifiedBy,
            LinkedIssues = linkedIssues,
            LinkedIssuesLastUpdate = linkedIssuesTimestamp,
            LinkedIssuesModifiedBy = linkedIssuesModifiedBy,
            ParentIssues = parentIssues,
            AssignedTo = assignedTo,
            AssignedToLastUpdate = assignedToTimestamp,
            AssignedToModifiedBy = assignedToModifiedBy,
            Tags = tags,
            TagsLastUpdate = tagsTimestamp,
            TagsModifiedBy = tagsModifiedBy,
            ExecutionMode = executionMode ?? ExecutionMode.Series,
            ExecutionModeLastUpdate = executionModeTimestamp,
            ExecutionModeModifiedBy = executionModeModifiedBy,
            WorkingBranchId = workingBranchId,
            WorkingBranchIdLastUpdate = workingBranchIdTimestamp,
            WorkingBranchIdModifiedBy = workingBranchIdModifiedBy,
            CreatedBy = createdBy,
            CreatedByLastUpdate = createdByTimestamp,
            LastUpdate = lastUpdate,
            CreatedAt = createdAt
        };

        return new LegacyMergeResult
        {
            MergedIssue = mergedIssue,
            PropertyChanges = changes
        };
    }

    private static (T Value, DateTimeOffset Timestamp, string? ModifiedBy, PropertyChange? Change) MergeProperty<T>(
        string propertyName,
        T valueA, DateTimeOffset timestampA, string? modifiedByA,
        T valueB, DateTimeOffset timestampB, string? modifiedByB,
        string? mergedBy, DateTimeOffset mergeTime)
        where T : notnull
    {
        if (EqualityComparer<T>.Default.Equals(valueA, valueB))
        {
            var newerTimestamp = timestampA > timestampB ? timestampA : timestampB;
            var newerModifiedBy = timestampA > timestampB ? modifiedByA : modifiedByB;
            return (valueA, newerTimestamp, newerModifiedBy, null);
        }

        var aWins = timestampA >= timestampB;
        var winner = aWins ? valueA : valueB;
        var winnerTimestamp = aWins ? timestampA : timestampB;
        var winnerModifiedBy = mergedBy ?? (aWins ? modifiedByA : modifiedByB);

        var change = new PropertyChange
        {
            PropertyName = propertyName,
            OldValue = (aWins ? valueB : valueA)?.ToString(),
            NewValue = winner?.ToString(),
            Timestamp = mergeTime,
            MergeResolution = aWins ? "A" : "B"
        };

        return (winner, winnerTimestamp, winnerModifiedBy, change);
    }

    private static (T? Value, DateTimeOffset? Timestamp, string? ModifiedBy, PropertyChange? Change) MergeNullableProperty<T>(
        string propertyName,
        T? valueA, DateTimeOffset? timestampA, string? modifiedByA,
        T? valueB, DateTimeOffset? timestampB, string? modifiedByB,
        string? mergedBy, DateTimeOffset mergeTime)
    {
        if (valueA is null && valueB is null)
        {
            return (default, null, null, null);
        }

        if (EqualityComparer<T>.Default.Equals(valueA, valueB))
        {
            var newerTimestamp = (timestampA ?? DateTimeOffset.MinValue) > (timestampB ?? DateTimeOffset.MinValue)
                ? timestampA
                : timestampB;
            var newerModifiedBy = (timestampA ?? DateTimeOffset.MinValue) > (timestampB ?? DateTimeOffset.MinValue)
                ? modifiedByA
                : modifiedByB;
            return (valueA, newerTimestamp, newerModifiedBy, null);
        }

        var effectiveTimestampA = timestampA ?? DateTimeOffset.MinValue;
        var effectiveTimestampB = timestampB ?? DateTimeOffset.MinValue;
        var aWins = effectiveTimestampA >= effectiveTimestampB;

        var winner = aWins ? valueA : valueB;
        var winnerTimestamp = aWins ? timestampA : timestampB;
        var winnerModifiedBy = mergedBy ?? (aWins ? modifiedByA : modifiedByB);

        var change = new PropertyChange
        {
            PropertyName = propertyName,
            OldValue = (aWins ? valueB : valueA)?.ToString(),
            NewValue = winner?.ToString(),
            Timestamp = mergeTime,
            MergeResolution = aWins ? "A" : "B"
        };

        return (winner, winnerTimestamp, winnerModifiedBy, change);
    }

    private static (IReadOnlyList<string> Value, DateTimeOffset Timestamp, string? ModifiedBy, PropertyChange? Change) MergeStringCollections(
        string propertyName,
        IReadOnlyList<string>? listA, DateTimeOffset timestampA, string? modifiedByA,
        IReadOnlyList<string>? listB, DateTimeOffset timestampB, string? modifiedByB,
        string? mergedBy, DateTimeOffset mergeTime)
    {
        var safeListA = listA ?? [];
        var safeListB = listB ?? [];

        var union = safeListA.Union(safeListB, StringComparer.OrdinalIgnoreCase).ToList();
        var newerTimestamp = timestampA > timestampB ? timestampA : timestampB;

        var setA = new HashSet<string>(safeListA, StringComparer.OrdinalIgnoreCase);
        var setB = new HashSet<string>(safeListB, StringComparer.OrdinalIgnoreCase);

        if (setA.SetEquals(setB))
        {
            var modifiedBy = timestampA > timestampB ? modifiedByA : modifiedByB;
            return (union, newerTimestamp, modifiedBy, null);
        }

        var change = new PropertyChange
        {
            PropertyName = propertyName,
            OldValue = $"A: [{string.Join(", ", safeListA)}], B: [{string.Join(", ", safeListB)}]",
            NewValue = string.Join(", ", union),
            Timestamp = mergeTime,
            MergeResolution = "Union"
        };

        return (union, newerTimestamp, mergedBy ?? (timestampA > timestampB ? modifiedByA : modifiedByB), change);
    }

    private static (IReadOnlyList<LegacyParentIssueRef> Value, PropertyChange? Change) MergeParentIssueCollections(
        string propertyName,
        IReadOnlyList<LegacyParentIssueRef>? listA,
        IReadOnlyList<LegacyParentIssueRef>? listB,
        string? mergedBy, DateTimeOffset mergeTime)
    {
        var safeListA = listA ?? [];
        var safeListB = listB ?? [];

        var dictA = safeListA
            .GroupBy(p => p.ParentIssue, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var dictB = safeListB
            .GroupBy(p => p.ParentIssue, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var allParentIds = new HashSet<string>(dictA.Keys.Concat(dictB.Keys), StringComparer.OrdinalIgnoreCase);
        var merged = new List<LegacyParentIssueRef>();

        foreach (var parentId in allParentIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
        {
            var inA = dictA.TryGetValue(parentId, out var refA);
            var inB = dictB.TryGetValue(parentId, out var refB);

            if (inA && inB)
            {
                var winner = refA!.LastUpdated >= refB!.LastUpdated ? refA : refB;
                merged.Add(winner);
            }
            else if (inA)
            {
                merged.Add(refA!);
            }
            else
            {
                merged.Add(refB!);
            }
        }

        if (safeListA.SequenceEqual(safeListB))
        {
            return (merged, null);
        }

        var change = new PropertyChange
        {
            PropertyName = propertyName,
            OldValue = $"A: [{string.Join(", ", safeListA.Select(p => $"{p.ParentIssue}({(p.Active ? "active" : "inactive")})"))}], B: [{string.Join(", ", safeListB.Select(p => $"{p.ParentIssue}({(p.Active ? "active" : "inactive")})"))}]",
            NewValue = string.Join(", ", merged.Select(p => $"{p.ParentIssue}({(p.Active ? "active" : "inactive")})")),
            Timestamp = mergeTime,
            MergeResolution = "PerParentLastWriteWins"
        };

        return (merged, change);
    }

    private static (string? Value, DateTimeOffset? Timestamp) MergeCreatedBy(
        string? valueA, DateTimeOffset? timestampA,
        string? valueB, DateTimeOffset? timestampB)
    {
        if (valueA is null && valueB is null)
        {
            return (null, null);
        }

        if (valueA is null)
        {
            return (valueB, timestampB);
        }

        if (valueB is null)
        {
            return (valueA, timestampA);
        }

        var effectiveTimestampA = timestampA ?? DateTimeOffset.MaxValue;
        var effectiveTimestampB = timestampB ?? DateTimeOffset.MaxValue;

        if (effectiveTimestampA <= effectiveTimestampB)
        {
            return (valueA, timestampA);
        }

        return (valueB, timestampB);
    }
}

/// <summary>
/// Result of a legacy property-by-property merge.
/// </summary>
public sealed record LegacyMergeResult
{
    public required LegacyIssue MergedIssue { get; init; }
    public required IReadOnlyList<PropertyChange> PropertyChanges { get; init; }
    public bool HadConflicts => PropertyChanges.Count > 0;
}
