using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class MigrationService(IStorageService storage) : IMigrationService
{
    public async Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var issues = (await storage.LoadIssuesAsync(cancellationToken)).ToList();

        if (issues.Count == 0)
        {
            return new MigrationResult
            {
                TotalIssues = 0,
                MigratedIssues = 0,
                AlreadyMigratedIssues = 0
            };
        }

        var migratedIssues = new List<Issue>();
        var migratedCount = 0;
        var alreadyMigratedCount = 0;

        foreach (var issue in issues)
        {
            if (NeedsMigration(issue))
            {
                var migrated = MigrateIssue(issue);
                migratedIssues.Add(migrated);
                migratedCount++;
            }
            else
            {
                migratedIssues.Add(issue);
                alreadyMigratedCount++;
            }
        }

        if (migratedCount > 0)
        {
            await storage.SaveIssuesAsync(migratedIssues, cancellationToken);
        }

        return new MigrationResult
        {
            TotalIssues = issues.Count,
            MigratedIssues = migratedCount,
            AlreadyMigratedIssues = alreadyMigratedCount
        };
    }

    public async Task<bool> IsMigrationNeededAsync(CancellationToken cancellationToken = default)
    {
        var issues = await storage.LoadIssuesAsync(cancellationToken);
        return issues.Any(NeedsMigration);
    }

    private static bool NeedsMigration(Issue issue)
    {
        // An issue needs migration if it has no property timestamps set
        // (all property timestamps are default/zero)
        return issue.TitleLastUpdate == default &&
               issue.StatusLastUpdate == default &&
               issue.TypeLastUpdate == default &&
               issue.CreatedAt == default;
    }

    private static Issue MigrateIssue(Issue issue)
    {
        // Use LastUpdate as the timestamp for all properties
        var timestamp = issue.LastUpdate;

        return issue with
        {
            TitleLastUpdate = timestamp,
            DescriptionLastUpdate = issue.Description is not null ? timestamp : null,
            StatusLastUpdate = timestamp,
            TypeLastUpdate = timestamp,
            PriorityLastUpdate = issue.Priority is not null ? timestamp : null,
            LinkedPRLastUpdate = issue.LinkedPR is not null ? timestamp : null,
            LinkedIssuesLastUpdate = timestamp,
            ParentIssuesLastUpdate = timestamp,
            CreatedAt = timestamp // Best guess - use LastUpdate as CreatedAt
        };
    }
}
