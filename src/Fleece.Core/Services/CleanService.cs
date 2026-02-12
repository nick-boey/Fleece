using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class CleanService(
    IStorageService storage,
    IGitConfigService gitConfigService) : ICleanService
{
    public async Task<CleanResult> CleanAsync(
        bool includeComplete = false,
        bool includeClosed = false,
        bool includeArchived = false,
        bool stripReferences = true,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var issues = (await storage.LoadIssuesAsync(cancellationToken)).ToList();
        var existingTombstones = await storage.LoadTombstonesAsync(cancellationToken);

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
            return new CleanResult
            {
                CleanedTombstones = [],
                StrippedReferences = []
            };
        }

        var now = DateTimeOffset.UtcNow;
        var cleanedBy = gitConfigService.GetUserName() ?? "unknown";
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

                // Strip ParentIssues
                var danglingParents = issue.ParentIssues
                    .Where(pi => cleanedIds.Contains(pi.ParentIssue))
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

                    updated = updated with
                    {
                        ParentIssues = issue.ParentIssues
                            .Where(pi => !cleanedIds.Contains(pi.ParentIssue))
                            .ToList(),
                        ParentIssuesLastUpdate = now,
                        ParentIssuesModifiedBy = cleanedBy
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

        if (!dryRun)
        {
            // Save remaining issues
            await storage.SaveIssuesAsync(toKeep, cancellationToken);

            // Merge new tombstones with existing
            var allTombstones = existingTombstones.ToList();
            allTombstones.AddRange(newTombstones);

            // Deduplicate by IssueId, keeping newest
            var deduplicatedTombstones = allTombstones
                .GroupBy(t => t.IssueId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(t => t.CleanedAt).First())
                .ToList();

            await storage.SaveTombstonesAsync(deduplicatedTombstones, cancellationToken);
        }

        return new CleanResult
        {
            CleanedTombstones = newTombstones,
            StrippedReferences = strippedReferences
        };
    }
}
