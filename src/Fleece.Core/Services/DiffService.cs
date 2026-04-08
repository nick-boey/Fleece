using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

internal sealed class DiffService(IJsonlSerializer serializer) : IDiffService
{
    public async Task<DiffResult> CompareFilesAsync(
        string file1Path,
        string file2Path,
        CancellationToken cancellationToken = default)
    {
        var content1 = await File.ReadAllTextAsync(file1Path, cancellationToken);
        var content2 = await File.ReadAllTextAsync(file2Path, cancellationToken);

        var issues1 = serializer.DeserializeIssues(content1);
        var issues2 = serializer.DeserializeIssues(content2);

        // Deduplicate by issue ID, keeping newest version (consistent with LoadIssuesAsync)
        var dict1 = issues1
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.LastUpdate).First(), StringComparer.OrdinalIgnoreCase);
        var dict2 = issues2
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.LastUpdate).First(), StringComparer.OrdinalIgnoreCase);

        var modified = new List<(Issue, Issue)>();
        var onlyInFile1 = new List<Issue>();
        var onlyInFile2 = new List<Issue>();

        // Find modified and issues only in file1
        foreach (var kvp in dict1)
        {
            if (dict2.TryGetValue(kvp.Key, out var issue2))
            {
                // Both files have this issue - check if different
                if (!AreIssuesEqual(kvp.Value, issue2))
                {
                    modified.Add((kvp.Value, issue2));
                }
            }
            else
            {
                // Only in file1
                onlyInFile1.Add(kvp.Value);
            }
        }

        // Find issues only in file2
        foreach (var kvp in dict2)
        {
            if (!dict1.ContainsKey(kvp.Key))
            {
                onlyInFile2.Add(kvp.Value);
            }
        }

        return new DiffResult
        {
            Modified = modified,
            OnlyInFile1 = onlyInFile1,
            OnlyInFile2 = onlyInFile2
        };
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
