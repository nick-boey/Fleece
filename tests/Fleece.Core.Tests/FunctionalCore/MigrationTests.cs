using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.FunctionalCore;

[TestFixture]
public class MigrationTests
{
    private static Issue CreateIssueNeedingMigration(string id = "abc123")
    {
        // An issue that needs migration: all property timestamps are default
        return new Issue
        {
            Id = id,
            Title = "Test Issue",
            TitleLastUpdate = default,
            Status = IssueStatus.Open,
            StatusLastUpdate = default,
            Type = IssueType.Task,
            TypeLastUpdate = default,
            LinkedIssues = [],
            LinkedIssuesLastUpdate = default,
            ParentIssues = [],
            Tags = [],
            TagsLastUpdate = default,


            ExecutionMode = ExecutionMode.Series,
            ExecutionModeLastUpdate = default,
            LastUpdate = DateTimeOffset.UtcNow,
            CreatedAt = default
        };
    }

    #region IsMigrationNeeded Tests

    [Test]
    public void IsMigrationNeeded_ReturnsTrue_WhenIssuesNeedTimestampMigration()
    {
        var issue = CreateIssueNeedingMigration();

        var result = Migration.IsMigrationNeeded([issue]);

        result.Should().BeTrue();
    }

    [Test]
    public void IsMigrationNeeded_ReturnsFalse_WhenNothingNeeded()
    {
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();

        var result = Migration.IsMigrationNeeded([issue]);

        result.Should().BeFalse();
    }

    [Test]
    public void IsMigrationNeeded_ReturnsTrue_WhenLinkedPrNeedsMigration()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Test")
            .Build() with { LinkedPR = 42 };

        var result = Migration.IsMigrationNeeded([issue]);

        result.Should().BeTrue();
    }

    #endregion

    #region Migrate Tests

    [Test]
    public void Migrate_ReturnsEmpty_WhenNoIssues()
    {
        var result = Migration.Migrate([]);

        result.Should().BeEmpty();
    }

    [Test]
    public void Migrate_MigratesIssuesWithoutTimestamps()
    {
        var issue = CreateIssueNeedingMigration();

        var result = Migration.Migrate([issue]);

        result.Should().HaveCount(1);
        var migrated = result[0];
        migrated.TitleLastUpdate.Should().Be(issue.LastUpdate);
        migrated.StatusLastUpdate.Should().Be(issue.LastUpdate);
        migrated.TypeLastUpdate.Should().Be(issue.LastUpdate);
        migrated.CreatedAt.Should().Be(issue.LastUpdate);
    }

    [Test]
    public void Migrate_DoesNotModifyIssuesAlreadyMigrated()
    {
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();

        var result = Migration.Migrate([issue]);

        result.Should().HaveCount(1);
        result[0].Should().BeSameAs(issue);
    }

    [Test]
    public void Migrate_SetsTimestampsFromLastUpdate_WhenMigrating()
    {
        var timestamp = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var issue = new Issue
        {
            Id = "abc123",
            Title = "Test Issue",
            TitleLastUpdate = default,
            Description = "A description",
            Status = IssueStatus.Open,
            StatusLastUpdate = default,
            Type = IssueType.Task,
            TypeLastUpdate = default,
            LinkedIssues = [],
            LinkedIssuesLastUpdate = default,
            ParentIssues = [],
            Tags = [],
            TagsLastUpdate = default,


            ExecutionMode = ExecutionMode.Series,
            ExecutionModeLastUpdate = default,
            LastUpdate = timestamp,
            CreatedAt = default
        };

        var result = Migration.Migrate([issue]);

        var migrated = result[0];
        migrated.TitleLastUpdate.Should().Be(timestamp);
        migrated.StatusLastUpdate.Should().Be(timestamp);
        migrated.TypeLastUpdate.Should().Be(timestamp);
        migrated.DescriptionLastUpdate.Should().Be(timestamp);
        migrated.CreatedAt.Should().Be(timestamp);
    }

    #endregion

    #region MigrateLinkedPrToTags Tests

    [Test]
    public void MigrateLinkedPrToTags_ConvertsFieldToTag()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var issue = new Issue
        {
            Id = "test123",
            Title = "Test",
            TitleLastUpdate = timestamp,
            Status = IssueStatus.Open,
            StatusLastUpdate = timestamp,
            Type = IssueType.Task,
            TypeLastUpdate = timestamp,
            LinkedPR = 123,
            LinkedPRLastUpdate = timestamp,
            LinkedPRModifiedBy = "migrator",
            LinkedIssues = [],
            ParentIssues = [],
            Tags = [],
            TagsLastUpdate = timestamp,

            LastUpdate = timestamp,
            CreatedAt = timestamp
        };

        var result = Migration.MigrateLinkedPrToTags(issue);

        result.LinkedPR.Should().BeNull();
        result.LinkedPRLastUpdate.Should().BeNull();
        result.LinkedPRModifiedBy.Should().BeNull();
        result.Tags.Should().Contain("hsp-linked-pr=123");
        result.TagsLastUpdate.Should().Be(timestamp);
        result.TagsModifiedBy.Should().Be("migrator");
    }

    [Test]
    public void MigrateLinkedPrToTags_PreservesExistingTags()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Test")
            .WithTags("existing-tag", "backend")
            .Build() with
        {
            LinkedPR = 42,
            LinkedPRLastUpdate = timestamp,
            LinkedPRModifiedBy = "test-user"
        };

        var result = Migration.MigrateLinkedPrToTags(issue);

        result.Tags.Should().Contain("existing-tag");
        result.Tags.Should().Contain("backend");
        result.Tags.Should().Contain("hsp-linked-pr=42");
    }

    [Test]
    public void MigrateLinkedPrToTags_DoesNotModify_WhenNoLinkedPr()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Test")
            .Build() with { LinkedPR = null };

        var result = Migration.MigrateLinkedPrToTags(issue);

        result.LinkedPR.Should().BeNull();
        result.Tags.Should().NotContain(t => t.StartsWith("hsp-linked-pr="));
    }

    #endregion
}
