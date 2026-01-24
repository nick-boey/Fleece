using Fleece.Core.Models;

namespace Fleece.Core.Services;

/// <summary>
/// Merges two issues property-by-property based on individual property timestamps.
/// </summary>
public sealed class IssueMerger
{
    /// <summary>
    /// Merges two versions of the same issue, keeping the newer value for each property.
    /// Collections (LinkedIssues, ParentIssues) use union strategy.
    /// </summary>
    public MergeResult Merge(Issue issueA, Issue issueB, string? mergedBy = null)
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
        var (linkedIssues, linkedIssuesTimestamp, linkedIssuesModifiedBy, linkedIssuesChange) = MergeCollections(
            "LinkedIssues", issueA.LinkedIssues, issueA.LinkedIssuesLastUpdate, issueA.LinkedIssuesModifiedBy,
            issueB.LinkedIssues, issueB.LinkedIssuesLastUpdate, issueB.LinkedIssuesModifiedBy, mergedBy, now);
        if (linkedIssuesChange is not null)
        {
            changes.Add(linkedIssuesChange);
        }

        var (parentIssues, parentIssuesTimestamp, parentIssuesModifiedBy, parentIssuesChange) = MergeCollections(
            "ParentIssues", issueA.ParentIssues, issueA.ParentIssuesLastUpdate, issueA.ParentIssuesModifiedBy,
            issueB.ParentIssues, issueB.ParentIssuesLastUpdate, issueB.ParentIssuesModifiedBy, mergedBy, now);
        if (parentIssuesChange is not null)
        {
            changes.Add(parentIssuesChange);
        }

        // Merge Group
        var (group, groupTimestamp, groupModifiedBy, groupChange) = MergeNullableProperty(
            "Group", issueA.Group, issueA.GroupLastUpdate, issueA.GroupModifiedBy,
            issueB.Group, issueB.GroupLastUpdate, issueB.GroupModifiedBy, mergedBy, now);
        if (groupChange is not null)
        {
            changes.Add(groupChange);
        }

        // Merge AssignedTo
        var (assignedTo, assignedToTimestamp, assignedToModifiedBy, assignedToChange) = MergeNullableProperty(
            "AssignedTo", issueA.AssignedTo, issueA.AssignedToLastUpdate, issueA.AssignedToModifiedBy,
            issueB.AssignedTo, issueB.AssignedToLastUpdate, issueB.AssignedToModifiedBy, mergedBy, now);
        if (assignedToChange is not null)
        {
            changes.Add(assignedToChange);
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

        var mergedIssue = new Issue
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
            ParentIssuesLastUpdate = parentIssuesTimestamp,
            ParentIssuesModifiedBy = parentIssuesModifiedBy,
            Group = group,
            GroupLastUpdate = groupTimestamp,
            GroupModifiedBy = groupModifiedBy,
            AssignedTo = assignedTo,
            AssignedToLastUpdate = assignedToTimestamp,
            AssignedToModifiedBy = assignedToModifiedBy,
            CreatedBy = createdBy,
            CreatedByLastUpdate = createdByTimestamp,
            LastUpdate = lastUpdate,
            CreatedAt = createdAt
        };

        return new MergeResult
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
        // If values are equal, no conflict
        if (EqualityComparer<T>.Default.Equals(valueA, valueB))
        {
            var newerTimestamp = timestampA > timestampB ? timestampA : timestampB;
            var newerModifiedBy = timestampA > timestampB ? modifiedByA : modifiedByB;
            return (valueA, newerTimestamp, newerModifiedBy, null);
        }

        // Values differ - use timestamp to decide winner
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
        // If both are null, no conflict
        if (valueA is null && valueB is null)
        {
            return (default, null, null, null);
        }

        // If values are equal, no conflict
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

        // Values differ - use timestamp to decide winner
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

    private static (IReadOnlyList<string> Value, DateTimeOffset Timestamp, string? ModifiedBy, PropertyChange? Change) MergeCollections(
        string propertyName,
        IReadOnlyList<string>? listA, DateTimeOffset timestampA, string? modifiedByA,
        IReadOnlyList<string>? listB, DateTimeOffset timestampB, string? modifiedByB,
        string? mergedBy, DateTimeOffset mergeTime)
    {
        // Handle null collections
        var safeListA = listA ?? [];
        var safeListB = listB ?? [];

        // Union strategy: combine both lists
        var union = safeListA.Union(safeListB, StringComparer.OrdinalIgnoreCase).ToList();
        var newerTimestamp = timestampA > timestampB ? timestampA : timestampB;

        // Check if there's a conflict (lists are different)
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

    private static (string? Value, DateTimeOffset? Timestamp) MergeCreatedBy(
        string? valueA, DateTimeOffset? timestampA,
        string? valueB, DateTimeOffset? timestampB)
    {
        // Keep oldest non-null value (creator never changes)
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

        // Both have values - keep the oldest (earliest timestamp)
        var effectiveTimestampA = timestampA ?? DateTimeOffset.MaxValue;
        var effectiveTimestampB = timestampB ?? DateTimeOffset.MaxValue;

        if (effectiveTimestampA <= effectiveTimestampB)
        {
            return (valueA, timestampA);
        }

        return (valueB, timestampB);
    }
}
