using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class MergeService(
    IStorageService storage,
    IChangeService changeService,
    IGitConfigService gitConfigService,
    IJsonlSerializer serializer) : IMergeService
{
    private readonly IssueMerger _merger = new();

    public async Task<IReadOnlyList<ChangeRecord>> FindAndResolveDuplicatesAsync(bool dryRun = false, CancellationToken cancellationToken = default)
    {
        // Load all issues from all issue files
        var allIssues = new List<Issue>();
        var issueFiles = await storage.GetAllIssueFilesAsync(cancellationToken);

        foreach (var file in issueFiles)
        {
            var issues = await storage.LoadIssuesFromFileAsync(file, cancellationToken);
            allIssues.AddRange(issues);
        }

        var changeRecords = new List<ChangeRecord>();
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
                var allPropertyChanges = new List<PropertyChange>();

                for (var i = 1; i < versions.Count; i++)
                {
                    var mergeResult = _merger.Merge(merged, versions[i], currentUser);
                    merged = mergeResult.MergedIssue;

                    if (mergeResult.HadConflicts)
                    {
                        allPropertyChanges.AddRange(mergeResult.PropertyChanges);
                    }
                }

                mergedIssues.Add(merged);

                // Record merge change with property-level details
                if (allPropertyChanges.Count > 0)
                {
                    var changeRecord = new ChangeRecord
                    {
                        ChangeId = Guid.NewGuid(),
                        IssueId = group.Key,
                        Type = ChangeType.Merged,
                        ChangedBy = currentUser ?? "unknown",
                        ChangedAt = DateTimeOffset.UtcNow,
                        PropertyChanges = allPropertyChanges
                    };

                    changeRecords.Add(changeRecord);

                    // Only persist the change record if not in dry-run mode
                    if (!dryRun)
                    {
                        await changeService.AddAsync(changeRecord, cancellationToken);
                    }
                }
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
        }

        return changeRecords;
    }

    public async Task<IReadOnlyList<(Issue, Issue)>> CompareFilesAsync(
        string file1Path,
        string file2Path,
        CancellationToken cancellationToken = default)
    {
        var content1 = await File.ReadAllTextAsync(file1Path, cancellationToken);
        var content2 = await File.ReadAllTextAsync(file2Path, cancellationToken);

        var issues1 = serializer.DeserializeIssues(content1);
        var issues2 = serializer.DeserializeIssues(content2);

        var dict1 = issues1.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var dict2 = issues2.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        var differences = new List<(Issue, Issue)>();

        foreach (var kvp in dict1)
        {
            if (dict2.TryGetValue(kvp.Key, out var issue2))
            {
                // Both files have this issue - check if different
                if (!AreIssuesEqual(kvp.Value, issue2))
                {
                    differences.Add((kvp.Value, issue2));
                }
            }
        }

        return differences;
    }

    private static bool AreIssuesEqual(Issue a, Issue b)
    {
        return a.Id == b.Id &&
               a.Title == b.Title &&
               a.Description == b.Description &&
               a.Status == b.Status &&
               a.Type == b.Type &&
               a.Priority == b.Priority &&
               a.LinkedPR == b.LinkedPR &&
               a.LinkedIssues.SequenceEqual(b.LinkedIssues) &&
               a.ParentIssues.SequenceEqual(b.ParentIssues);
    }
}
