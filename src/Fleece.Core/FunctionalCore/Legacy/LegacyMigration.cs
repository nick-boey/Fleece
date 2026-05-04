using Fleece.Core.Models;
using Fleece.Core.Models.Legacy;

namespace Fleece.Core.FunctionalCore.Legacy;

/// <summary>
/// Legacy in-memory shape migration for <see cref="LegacyIssue"/>. Event-sourced storage
/// no longer requires this — kept for any remaining legacy import paths.
/// </summary>
public static class LegacyMigration
{
    public static bool IsMigrationNeeded(IReadOnlyList<LegacyIssue> issues)
    {
        return issues.Any(NeedsMigration);
    }

    public static IReadOnlyList<LegacyIssue> Migrate(IReadOnlyList<LegacyIssue> issues)
    {
        var result = new List<LegacyIssue>(issues.Count);

        foreach (var issue in issues)
        {
            result.Add(NeedsMigration(issue) ? MigrateIssue(issue) : issue);
        }

        return result;
    }

    private static bool NeedsMigration(LegacyIssue issue)
    {
        var needsTimestampMigration = issue.TitleLastUpdate == default &&
                                      issue.StatusLastUpdate == default &&
                                      issue.TypeLastUpdate == default &&
                                      issue.CreatedAt == default;

        var needsLinkedPrMigration = issue.LinkedPR.HasValue;

        var needsParentRefTimestampMigration = issue.ParentIssues?.Any(p => p.LastUpdated == default) ?? false;

        return needsTimestampMigration || needsLinkedPrMigration || needsParentRefTimestampMigration;
    }

    private static LegacyIssue MigrateIssue(LegacyIssue issue)
    {
        var result = issue;

        if (issue.TitleLastUpdate == default &&
            issue.StatusLastUpdate == default &&
            issue.TypeLastUpdate == default &&
            issue.CreatedAt == default)
        {
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
                ParentIssues = (issue.ParentIssues ?? []).Select(p =>
                    p.LastUpdated == default
                        ? p with { LastUpdated = timestamp, Active = true }
                        : p).ToList(),
                CreatedAt = timestamp
            };
        }

        if (result.LinkedPR.HasValue)
        {
            result = MigrateLinkedPrToTags(result);
        }

        // Migrate parent refs that lack per-parent timestamps
        if (result.ParentIssues?.Any(p => p.LastUpdated == default) == true)
        {
            var fallbackTimestamp = result.LastUpdate;
            result = result with
            {
                ParentIssues = result.ParentIssues.Select(p =>
                    p.LastUpdated == default
                        ? p with { LastUpdated = fallbackTimestamp, Active = true }
                        : p).ToList()
            };
        }

        return result;
    }

    /// <summary>
    /// Migrates the deprecated LinkedPR field to a keyed tag.
    /// </summary>
    public static LegacyIssue MigrateLinkedPrToTags(LegacyIssue issue)
    {
        if (!issue.LinkedPR.HasValue)
        {
            return issue;
        }

        var newTags = KeyedTag.AddValue(issue.Tags, KeyedTag.LinkedPrKey, issue.LinkedPR.Value.ToString());

        var tagsTimestamp = issue.LinkedPRLastUpdate ?? issue.TagsLastUpdate;
        if (tagsTimestamp == default)
        {
            tagsTimestamp = issue.LastUpdate;
        }

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
