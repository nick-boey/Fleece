using Fleece.Core.Models;

namespace Fleece.Core.FunctionalCore;

public sealed record CleanPlan
{
    public required IReadOnlyList<Issue> IssuesToRemove { get; init; }
    public required IReadOnlyList<Tombstone> TombstonesToCreate { get; init; }
    public required IReadOnlyList<Issue> UpdatedIssues { get; init; }
    public required IReadOnlyList<StrippedReference> StrippedReferences { get; init; }
}

public static class Cleaning
{
    public static CleanPlan Plan(
        IReadOnlyList<Issue> issues,
        IReadOnlyList<Tombstone> existingTombstones,
        bool includeComplete,
        bool includeClosed,
        bool includeArchived,
        bool stripReferences,
        DateTimeOffset now,
        string cleanedBy)
    {
        // Build target status set
        var targetStatuses = new HashSet<IssueStatus> { IssueStatus.Deleted };
        if (includeComplete)
        {
            targetStatuses.Add(IssueStatus.Complete);
        }

        if (includeClosed)
        {
            targetStatuses.Add(IssueStatus.Closed);
        }

        if (includeArchived)
        {
            targetStatuses.Add(IssueStatus.Archived);
        }

        // Partition issues
        var toClean = issues.Where(i => targetStatuses.Contains(i.Status)).ToList();
        var toKeep = issues.Where(i => !targetStatuses.Contains(i.Status)).ToList();

        if (toClean.Count == 0)
        {
            return new CleanPlan
            {
                IssuesToRemove = [],
                TombstonesToCreate = [],
                UpdatedIssues = toKeep,
                StrippedReferences = []
            };
        }

        var cleanedIds = toClean.Select(i => i.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Create tombstones
        var newTombstones = toClean.Select(i => new Tombstone
        {
            IssueId = i.Id,
            OriginalTitle = i.Title,
            CleanedAt = now,
            CleanedBy = cleanedBy
        }).ToList();

        // Strip dangling references
        var strippedReferences = new List<StrippedReference>();
        if (stripReferences)
        {
            for (var i = 0; i < toKeep.Count; i++)
            {
                var issue = toKeep[i];
                var updated = issue;
                var modified = false;

                // Strip LinkedIssues
                var danglingLinked = issue.LinkedIssues
                    .Where(li => cleanedIds.Contains(li))
                    .ToList();

                if (danglingLinked.Count > 0)
                {
                    foreach (var linkedId in danglingLinked)
                    {
                        strippedReferences.Add(new StrippedReference
                        {
                            IssueId = linkedId,
                            ReferencingIssueId = issue.Id,
                            ReferenceType = "LinkedIssues"
                        });
                    }

                    updated = updated with
                    {
                        LinkedIssues = issue.LinkedIssues
                            .Where(li => !cleanedIds.Contains(li))
                            .ToList(),
                        LinkedIssuesLastUpdate = now,
                        LinkedIssuesModifiedBy = cleanedBy
                    };
                    modified = true;
                }

                // Strip ParentIssues — soft-delete dangling refs
                var danglingParents = issue.ParentIssues
                    .Where(pi => cleanedIds.Contains(pi.ParentIssue) && pi.Active)
                    .ToList();

                if (danglingParents.Count > 0)
                {
                    foreach (var parent in danglingParents)
                    {
                        strippedReferences.Add(new StrippedReference
                        {
                            IssueId = parent.ParentIssue,
                            ReferencingIssueId = issue.Id,
                            ReferenceType = "ParentIssues"
                        });
                    }

                    var danglingParentIds = danglingParents
                        .Select(p => p.ParentIssue)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    updated = updated with
                    {
                        ParentIssues = issue.ParentIssues.Select(pi =>
                            danglingParentIds.Contains(pi.ParentIssue)
                                ? pi with { Active = false, LastUpdated = now, UpdatedBy = cleanedBy }
                                : pi).ToList()
                    };
                    modified = true;
                }

                if (modified)
                {
                    updated = updated with { LastUpdate = now };
                    toKeep[i] = updated;
                }
            }
        }

        return new CleanPlan
        {
            IssuesToRemove = toClean,
            TombstonesToCreate = newTombstones,
            UpdatedIssues = toKeep,
            StrippedReferences = strippedReferences
        };
    }
}
