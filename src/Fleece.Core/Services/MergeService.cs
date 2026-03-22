using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class MergeService(
    IStorageService storage,
    IGitConfigService gitConfigService) : IMergeService
{
    private readonly IssueMerger _merger = new();

    public async Task<int> FindAndResolveDuplicatesAsync(bool dryRun = false, CancellationToken cancellationToken = default)
    {
        // Load all issues from all issue files
        var allIssues = new List<Issue>();
        var issueFiles = await storage.GetAllIssueFilesAsync(cancellationToken);

        foreach (var file in issueFiles)
        {
            var issues = await storage.LoadIssuesFromFileAsync(file, cancellationToken);
            allIssues.AddRange(issues);
        }

        var mergedCount = 0;
        var currentUser = gitConfigService.GetUserName();

        // Group by ID to find duplicates
        var grouped = allIssues.GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var mergedIssues = new List<Issue>();

        foreach (var group in grouped)
        {
            var versions = group.ToList();

            if (versions.Count > 1)
            {
                // Merge all versions using property-level merging
                var merged = versions[0];

                for (var i = 1; i < versions.Count; i++)
                {
                    var mergeResult = _merger.Merge(merged, versions[i], currentUser);
                    merged = mergeResult.MergedIssue;
                }

                mergedIssues.Add(merged);
                mergedCount++;
            }
            else
            {
                mergedIssues.Add(versions[0]);
            }
        }

        // Only save merged issues and clean up old files if not in dry-run mode
        if (!dryRun && issueFiles.Count > 0)
        {
            // Delete all old issue files
            foreach (var file in issueFiles)
            {
                await storage.DeleteIssueFileAsync(file, cancellationToken);
            }

            // Save consolidated issues with new hash
            await storage.SaveIssuesWithHashAsync(mergedIssues, cancellationToken);

            // Consolidate tombstone files if multiple exist
            var tombstoneFiles = await storage.GetAllTombstoneFilesAsync(cancellationToken);
            if (tombstoneFiles.Count > 1)
            {
                var allTombstones = await storage.LoadTombstonesAsync(cancellationToken);
                await storage.SaveTombstonesAsync(allTombstones, cancellationToken);
            }
        }

        return mergedCount;
    }

}
