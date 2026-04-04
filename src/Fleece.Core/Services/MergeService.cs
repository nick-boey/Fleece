using Fleece.Core.FunctionalCore;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class MergeService(
    IStorageService storage,
    IGitConfigService gitConfigService) : IMergeService
{
    public async Task<int> FindAndResolveDuplicatesAsync(bool dryRun = false, CancellationToken cancellationToken = default)
    {
        // Load all issues from all issue files
        var issueFiles = await storage.GetAllIssueFilesAsync(cancellationToken);
        var fileGroups = new List<(string filePath, IReadOnlyList<Models.Issue> issues)>();

        foreach (var file in issueFiles)
        {
            var issues = await storage.LoadIssuesFromFileAsync(file, cancellationToken);
            fileGroups.Add((file, issues));
        }

        var currentUser = gitConfigService.GetUserName();

        // Delegate to pure functions
        var plan = Merging.Plan(fileGroups, currentUser);
        var mergedIssues = Merging.Apply(plan);

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

        return plan.DuplicateCount;
    }

}
