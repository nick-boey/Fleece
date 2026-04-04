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

        var migratedIssues = FunctionalCore.Migration.Migrate(issues);

        var migratedCount = 0;
        var alreadyMigratedCount = 0;
        for (var i = 0; i < issues.Count; i++)
        {
            if (!ReferenceEquals(issues[i], migratedIssues[i]))
            {
                migratedCount++;
            }
            else
            {
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
        var needsTimestampMigration = FunctionalCore.Migration.IsMigrationNeeded(loadResult.Issues);

        return needsTimestampMigration || hasUnknownProperties;
    }

    /// <summary>
    /// Migrates the deprecated LinkedPR field to a keyed tag.
    /// </summary>
    public static Issue MigrateLinkedPrToTags(Issue issue)
    {
        return FunctionalCore.Migration.MigrateLinkedPrToTags(issue);
    }
}
