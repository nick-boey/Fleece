using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class MigrationService(IStorageService storage) : IMigrationService
{
    public async Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var loadResult = await storage.LoadIssuesWithDiagnosticsAsync(cancellationToken);
        var issues = loadResult.Issues;

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

        // Collect all unknown properties across all files
        var unknownProperties = loadResult.Diagnostics
            .SelectMany(d => d.UnknownProperties)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Save if any timestamp migration happened OR unknown properties need cleaning
        if (migratedCount > 0 || unknownProperties.Count > 0)
        {
            await storage.SaveIssuesAsync(migratedIssues, cancellationToken);
        }

        return new MigrationResult
        {
            TotalIssues = issues.Count,
            MigratedIssues = migratedCount,
            AlreadyMigratedIssues = alreadyMigratedCount,
            UnknownPropertiesDeleted = unknownProperties
        };
    }

    public async Task<bool> IsMigrationNeededAsync(CancellationToken cancellationToken = default)
    {
        var loadResult = await storage.LoadIssuesWithDiagnosticsAsync(cancellationToken);

        // Migration is needed if any issue lacks timestamps OR any file has unknown properties
        var hasUnknownProperties = loadResult.Diagnostics.Any(d => d.UnknownProperties.Count > 0);
        var needsTimestampMigration = loadResult.Issues.Any(NeedsMigration);

        return needsTimestampMigration || hasUnknownProperties;
    }

    private static bool NeedsMigration(Issue issue)
    {
        // An issue needs migration if:
        // 1. It has no property timestamps set (all property timestamps are default/zero)
        // 2. It has the deprecated LinkedPR field set (needs to be migrated to tags)
        var needsTimestampMigration = issue.TitleLastUpdate == default &&
                                      issue.StatusLastUpdate == default &&
                                      issue.TypeLastUpdate == default &&
                                      issue.CreatedAt == default;

        var needsLinkedPrMigration = issue.LinkedPR.HasValue;

        return needsTimestampMigration || needsLinkedPrMigration;
    }

    private static Issue MigrateIssue(Issue issue)
    {
        var result = issue;

        // Migrate timestamps if needed
        if (issue.TitleLastUpdate == default &&
            issue.StatusLastUpdate == default &&
            issue.TypeLastUpdate == default &&
            issue.CreatedAt == default)
        {
            // Use LastUpdate as the timestamp for all properties
            var timestamp = issue.LastUpdate;

            result = result with
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

        // Migrate LinkedPR to tags if needed
        if (result.LinkedPR.HasValue)
        {
            result = MigrateLinkedPrToTags(result);
        }

        return result;
    }

    /// <summary>
    /// Migrates the deprecated LinkedPR field to a keyed tag.
    /// </summary>
    public static Issue MigrateLinkedPrToTags(Issue issue)
    {
        if (!issue.LinkedPR.HasValue)
        {
            return issue;
        }

        var newTags = KeyedTag.AddValue(issue.Tags, KeyedTag.LinkedPrKey, issue.LinkedPR.Value.ToString());

        // Determine the tags timestamp - use LinkedPR timestamp if available, otherwise use existing tags timestamp
        var tagsTimestamp = issue.LinkedPRLastUpdate ?? issue.TagsLastUpdate;
        if (tagsTimestamp == default)
        {
            tagsTimestamp = issue.LastUpdate;
        }

        // Determine the tags modified by - prefer LinkedPR modifier if available
        var tagsModifiedBy = issue.LinkedPRModifiedBy ?? issue.TagsModifiedBy;

        return issue with
        {
            Tags = newTags,
            TagsLastUpdate = tagsTimestamp,
            TagsModifiedBy = tagsModifiedBy,
            LinkedPR = null,
            LinkedPRLastUpdate = null,
            LinkedPRModifiedBy = null
        };
    }
}
