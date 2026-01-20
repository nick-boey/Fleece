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
    public MergeResult Merge(Issue issueA, Issue issueB)
    {
        if (!issueA.Id.Equals(issueB.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Cannot merge issues with different IDs: '{issueA.Id}' and '{issueB.Id}'");
        }

        var conflicts = new List<PropertyConflict>();

        // Merge Title
        var (title, titleTimestamp, titleConflict) = MergeProperty(
            "Title", issueA.Title, issueA.TitleLastUpdate, issueB.Title, issueB.TitleLastUpdate);
        if (titleConflict is not null)
        {
            conflicts.Add(titleConflict);
        }

        // Merge Description
        var (description, descriptionTimestamp, descriptionConflict) = MergeNullableProperty(
            "Description", issueA.Description, issueA.DescriptionLastUpdate, issueB.Description, issueB.DescriptionLastUpdate);
        if (descriptionConflict is not null)
        {
            conflicts.Add(descriptionConflict);
        }

        // Merge Status
        var (status, statusTimestamp, statusConflict) = MergeProperty(
            "Status", issueA.Status, issueA.StatusLastUpdate, issueB.Status, issueB.StatusLastUpdate);
        if (statusConflict is not null)
        {
            conflicts.Add(statusConflict);
        }

        // Merge Type
        var (type, typeTimestamp, typeConflict) = MergeProperty(
            "Type", issueA.Type, issueA.TypeLastUpdate, issueB.Type, issueB.TypeLastUpdate);
        if (typeConflict is not null)
        {
            conflicts.Add(typeConflict);
        }

        // Merge Priority
        var (priority, priorityTimestamp, priorityConflict) = MergeNullableProperty(
            "Priority", issueA.Priority, issueA.PriorityLastUpdate, issueB.Priority, issueB.PriorityLastUpdate);
        if (priorityConflict is not null)
        {
            conflicts.Add(priorityConflict);
        }

        // Merge LinkedPR
        var (linkedPR, linkedPRTimestamp, linkedPRConflict) = MergeNullableProperty(
            "LinkedPR", issueA.LinkedPR, issueA.LinkedPRLastUpdate, issueB.LinkedPR, issueB.LinkedPRLastUpdate);
        if (linkedPRConflict is not null)
        {
            conflicts.Add(linkedPRConflict);
        }

        // Merge collections with union strategy
        var (linkedIssues, linkedIssuesTimestamp, linkedIssuesConflict) = MergeCollections(
            "LinkedIssues", issueA.LinkedIssues, issueA.LinkedIssuesLastUpdate, issueB.LinkedIssues, issueB.LinkedIssuesLastUpdate);
        if (linkedIssuesConflict is not null)
        {
            conflicts.Add(linkedIssuesConflict);
        }

        var (parentIssues, parentIssuesTimestamp, parentIssuesConflict) = MergeCollections(
            "ParentIssues", issueA.ParentIssues, issueA.ParentIssuesLastUpdate, issueB.ParentIssues, issueB.ParentIssuesLastUpdate);
        if (parentIssuesConflict is not null)
        {
            conflicts.Add(parentIssuesConflict);
        }

        // Merge Group
        var (group, groupTimestamp, groupConflict) = MergeNullableProperty(
            "Group", issueA.Group, issueA.GroupLastUpdate, issueB.Group, issueB.GroupLastUpdate);
        if (groupConflict is not null)
        {
            conflicts.Add(groupConflict);
        }

        // Merge AssignedTo
        var (assignedTo, assignedToTimestamp, assignedToConflict) = MergeNullableProperty(
            "AssignedTo", issueA.AssignedTo, issueA.AssignedToLastUpdate, issueB.AssignedTo, issueB.AssignedToLastUpdate);
        if (assignedToConflict is not null)
        {
            conflicts.Add(assignedToConflict);
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
            Description = description,
            DescriptionLastUpdate = descriptionTimestamp,
            Status = status,
            StatusLastUpdate = statusTimestamp,
            Type = type,
            TypeLastUpdate = typeTimestamp,
            Priority = priority,
            PriorityLastUpdate = priorityTimestamp,
            LinkedPR = linkedPR,
            LinkedPRLastUpdate = linkedPRTimestamp,
            LinkedIssues = linkedIssues,
            LinkedIssuesLastUpdate = linkedIssuesTimestamp,
            ParentIssues = parentIssues,
            ParentIssuesLastUpdate = parentIssuesTimestamp,
            Group = group,
            GroupLastUpdate = groupTimestamp,
            AssignedTo = assignedTo,
            AssignedToLastUpdate = assignedToTimestamp,
            CreatedBy = createdBy,
            CreatedByLastUpdate = createdByTimestamp,
            LastUpdate = lastUpdate,
            CreatedAt = createdAt
        };

        return new MergeResult
        {
            MergedIssue = mergedIssue,
            PropertyConflicts = conflicts
        };
    }

    private static (T Value, DateTimeOffset Timestamp, PropertyConflict? Conflict) MergeProperty<T>(
        string propertyName,
        T valueA, DateTimeOffset timestampA,
        T valueB, DateTimeOffset timestampB)
        where T : notnull
    {
        // If values are equal, no conflict
        if (EqualityComparer<T>.Default.Equals(valueA, valueB))
        {
            var newerTimestamp = timestampA > timestampB ? timestampA : timestampB;
            return (valueA, newerTimestamp, null);
        }

        // Values differ - use timestamp to decide winner
        var aWins = timestampA >= timestampB;
        var winner = aWins ? valueA : valueB;
        var winnerTimestamp = aWins ? timestampA : timestampB;

        var conflict = new PropertyConflict
        {
            PropertyName = propertyName,
            ValueA = valueA?.ToString(),
            TimestampA = timestampA,
            ValueB = valueB?.ToString(),
            TimestampB = timestampB,
            ResolvedValue = winner?.ToString(),
            Resolution = aWins ? "A" : "B"
        };

        return (winner, winnerTimestamp, conflict);
    }

    private static (T? Value, DateTimeOffset? Timestamp, PropertyConflict? Conflict) MergeNullableProperty<T>(
        string propertyName,
        T? valueA, DateTimeOffset? timestampA,
        T? valueB, DateTimeOffset? timestampB)
    {
        // If both are null, no conflict
        if (valueA is null && valueB is null)
        {
            return (default, null, null);
        }

        // If values are equal, no conflict
        if (EqualityComparer<T>.Default.Equals(valueA, valueB))
        {
            var newerTimestamp = (timestampA ?? DateTimeOffset.MinValue) > (timestampB ?? DateTimeOffset.MinValue)
                ? timestampA
                : timestampB;
            return (valueA, newerTimestamp, null);
        }

        // Values differ - use timestamp to decide winner
        var effectiveTimestampA = timestampA ?? DateTimeOffset.MinValue;
        var effectiveTimestampB = timestampB ?? DateTimeOffset.MinValue;
        var aWins = effectiveTimestampA >= effectiveTimestampB;

        var winner = aWins ? valueA : valueB;
        var winnerTimestamp = aWins ? timestampA : timestampB;

        var conflict = new PropertyConflict
        {
            PropertyName = propertyName,
            ValueA = valueA?.ToString(),
            TimestampA = timestampA,
            ValueB = valueB?.ToString(),
            TimestampB = timestampB,
            ResolvedValue = winner?.ToString(),
            Resolution = aWins ? "A" : "B"
        };

        return (winner, winnerTimestamp, conflict);
    }

    private static (IReadOnlyList<string> Value, DateTimeOffset Timestamp, PropertyConflict? Conflict) MergeCollections(
        string propertyName,
        IReadOnlyList<string> listA, DateTimeOffset timestampA,
        IReadOnlyList<string> listB, DateTimeOffset timestampB)
    {
        // Union strategy: combine both lists
        var union = listA.Union(listB, StringComparer.OrdinalIgnoreCase).ToList();
        var newerTimestamp = timestampA > timestampB ? timestampA : timestampB;

        // Check if there's a conflict (lists are different)
        var setA = new HashSet<string>(listA, StringComparer.OrdinalIgnoreCase);
        var setB = new HashSet<string>(listB, StringComparer.OrdinalIgnoreCase);

        if (setA.SetEquals(setB))
        {
            return (union, newerTimestamp, null);
        }

        var conflict = new PropertyConflict
        {
            PropertyName = propertyName,
            ValueA = string.Join(", ", listA),
            TimestampA = timestampA,
            ValueB = string.Join(", ", listB),
            TimestampB = timestampB,
            ResolvedValue = string.Join(", ", union),
            Resolution = "Union"
        };

        return (union, newerTimestamp, conflict);
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
