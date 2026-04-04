using Fleece.Core.FunctionalCore;
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

        var now = DateTimeOffset.UtcNow;
        var cleanedBy = gitConfigService.GetUserName() ?? "unknown";

        var plan = Cleaning.Plan(
            issues,
            existingTombstones.ToList(),
            includeComplete,
            includeClosed,
            includeArchived,
            stripReferences,
            now,
            cleanedBy);

        if (plan.IssuesToRemove.Count == 0)
        {
            return new CleanResult
            {
                CleanedTombstones = [],
                StrippedReferences = []
            };
        }

        if (!dryRun)
        {
            // Save remaining issues
            await storage.SaveIssuesAsync(plan.UpdatedIssues.ToList(), cancellationToken);

            // Merge new tombstones with existing
            var allTombstones = existingTombstones.ToList();
            allTombstones.AddRange(plan.TombstonesToCreate);

            // Deduplicate by IssueId, keeping newest
            var deduplicatedTombstones = allTombstones
                .GroupBy(t => t.IssueId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(t => t.CleanedAt).First())
                .ToList();

            await storage.SaveTombstonesAsync(deduplicatedTombstones, cancellationToken);
        }

        return new CleanResult
        {
            CleanedTombstones = plan.TombstonesToCreate.ToList(),
            StrippedReferences = plan.StrippedReferences.ToList()
        };
    }
}
