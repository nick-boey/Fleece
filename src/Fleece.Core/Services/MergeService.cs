using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class MergeService(
    IStorageService storage,
    IConflictService conflictService,
    IJsonlSerializer serializer) : IMergeService
{
    public async Task<IReadOnlyList<ConflictRecord>> FindAndResolveDuplicatesAsync(CancellationToken cancellationToken = default)
    {
        var issues = (await storage.LoadIssuesAsync(cancellationToken)).ToList();
        var conflicts = new List<ConflictRecord>();

        // Group by ID to find duplicates
        var grouped = issues.GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var deduped = new List<Issue>();

        foreach (var group in grouped)
        {
            var sorted = group.OrderByDescending(i => i.LastUpdate).ToList();

            if (sorted.Count > 1)
            {
                // Keep the newest, move others to conflicts
                var newest = sorted[0];
                deduped.Add(newest);

                for (var i = 1; i < sorted.Count; i++)
                {
                    var older = sorted[i];
                    var conflict = new ConflictRecord
                    {
                        ConflictId = Guid.NewGuid(),
                        IssueId = group.Key,
                        OlderVersion = older,
                        NewerVersion = newest,
                        DetectedAt = DateTimeOffset.UtcNow
                    };

                    conflicts.Add(conflict);
                    await conflictService.AddAsync(conflict, cancellationToken);
                }
            }
            else
            {
                deduped.Add(sorted[0]);
            }
        }

        if (conflicts.Count > 0)
        {
            await storage.SaveIssuesAsync(deduped, cancellationToken);
        }

        return conflicts;
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
