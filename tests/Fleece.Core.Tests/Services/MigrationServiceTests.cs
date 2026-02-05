using Fleece.Core.Models;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class MigrationServiceTests
{
    private IStorageService _storage = null!;
    private MigrationService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _storage = Substitute.For<IStorageService>();
        _sut = new MigrationService(_storage);
    }

    private void SetupLoadResult(
        IReadOnlyList<Issue>? issues = null,
        IReadOnlyList<ParseDiagnostic>? diagnostics = null)
    {
        var result = new LoadIssuesResult
        {
            Issues = issues ?? [],
            Diagnostics = diagnostics ?? []
        };

        _storage.LoadIssuesWithDiagnosticsAsync(Arg.Any<CancellationToken>())
            .Returns(result);
    }

    private static ParseDiagnostic CreateDiagnostic(
        IReadOnlySet<string>? unknownProperties = null,
        int totalRows = 1,
        int parsedRows = 1)
    {
        return new ParseDiagnostic
        {
            FilePath = "issues_abc123.jsonl",
            TotalRows = totalRows,
            ParsedRows = parsedRows,
            FailedRows = 0,
            UnknownProperties = unknownProperties ?? new HashSet<string>()
        };
    }

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
            ParentIssuesLastUpdate = default,
            Tags = [],
            TagsLastUpdate = default,
            Questions = [],
            QuestionsLastUpdate = default,
            ExecutionMode = ExecutionMode.Series,
            ExecutionModeLastUpdate = default,
            LastUpdate = DateTimeOffset.UtcNow,
            CreatedAt = default
        };
    }

    #region MigrateAsync Tests

    [Test]
    public async Task MigrateAsync_ReturnsZeroCounts_WhenNoIssues()
    {
        SetupLoadResult();

        var result = await _sut.MigrateAsync();

        result.TotalIssues.Should().Be(0);
        result.MigratedIssues.Should().Be(0);
        result.AlreadyMigratedIssues.Should().Be(0);
        result.WasMigrationNeeded.Should().BeFalse();
        result.UnknownPropertiesDeleted.Should().BeEmpty();
    }

    [Test]
    public async Task MigrateAsync_MigratesIssuesWithoutTimestamps()
    {
        var issue = CreateIssueNeedingMigration();
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic()]);

        var result = await _sut.MigrateAsync();

        result.TotalIssues.Should().Be(1);
        result.MigratedIssues.Should().Be(1);
        result.AlreadyMigratedIssues.Should().Be(0);
        result.WasMigrationNeeded.Should().BeTrue();
        await _storage.Received(1).SaveIssuesAsync(
            Arg.Any<IReadOnlyList<Issue>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MigrateAsync_DoesNotMigrateIssuesWithTimestamps()
    {
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic()]);

        var result = await _sut.MigrateAsync();

        result.TotalIssues.Should().Be(1);
        result.MigratedIssues.Should().Be(0);
        result.AlreadyMigratedIssues.Should().Be(1);
    }

    [Test]
    public async Task MigrateAsync_SavesWhenOnlyUnknownPropertiesExist()
    {
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();
        var unknownProps = new HashSet<string> { "obsoleteField" };
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic(unknownProperties: unknownProps)]);

        var result = await _sut.MigrateAsync();

        result.MigratedIssues.Should().Be(0);
        result.WasMigrationNeeded.Should().BeTrue();
        await _storage.Received(1).SaveIssuesAsync(
            Arg.Any<IReadOnlyList<Issue>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MigrateAsync_ReportsUnknownPropertiesDeleted()
    {
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();
        var unknownProps = new HashSet<string> { "oldField", "deprecatedProp" };
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic(unknownProperties: unknownProps)]);

        var result = await _sut.MigrateAsync();

        result.UnknownPropertiesDeleted.Should().Contain("oldField");
        result.UnknownPropertiesDeleted.Should().Contain("deprecatedProp");
        result.UnknownPropertiesDeleted.Should().HaveCount(2);
    }

    [Test]
    public async Task MigrateAsync_DoesNotSave_WhenNoMigrationAndNoUnknownProperties()
    {
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic()]);

        await _sut.MigrateAsync();

        await _storage.DidNotReceive().SaveIssuesAsync(
            Arg.Any<IReadOnlyList<Issue>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MigrateAsync_WasMigrationNeeded_TrueWhenTimestampMigrationNeeded()
    {
        var issue = CreateIssueNeedingMigration();
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic()]);

        var result = await _sut.MigrateAsync();

        result.WasMigrationNeeded.Should().BeTrue();
    }

    [Test]
    public async Task MigrateAsync_WasMigrationNeeded_TrueWhenOnlyUnknownPropertiesExist()
    {
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();
        var unknownProps = new HashSet<string> { "unknownField" };
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic(unknownProperties: unknownProps)]);

        var result = await _sut.MigrateAsync();

        result.WasMigrationNeeded.Should().BeTrue();
    }

    [Test]
    public async Task MigrateAsync_WasMigrationNeeded_FalseWhenNothingNeeded()
    {
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic()]);

        var result = await _sut.MigrateAsync();

        result.WasMigrationNeeded.Should().BeFalse();
    }

    [Test]
    public async Task MigrateAsync_CombinesUnknownPropertiesFromMultipleFiles()
    {
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();
        var unknownProps1 = new HashSet<string> { "field1" };
        var unknownProps2 = new HashSet<string> { "field2", "field3" };
        var diag1 = new ParseDiagnostic
        {
            FilePath = "issues_aaa.jsonl",
            TotalRows = 1,
            ParsedRows = 1,
            FailedRows = 0,
            UnknownProperties = unknownProps1
        };
        var diag2 = new ParseDiagnostic
        {
            FilePath = "issues_bbb.jsonl",
            TotalRows = 1,
            ParsedRows = 1,
            FailedRows = 0,
            UnknownProperties = unknownProps2
        };
        SetupLoadResult(
            issues: [issue],
            diagnostics: [diag1, diag2]);

        var result = await _sut.MigrateAsync();

        result.UnknownPropertiesDeleted.Should().HaveCount(3);
        result.UnknownPropertiesDeleted.Should().Contain("field1");
        result.UnknownPropertiesDeleted.Should().Contain("field2");
        result.UnknownPropertiesDeleted.Should().Contain("field3");
    }

    [Test]
    public async Task MigrateAsync_SetsTimestampsFromLastUpdate_WhenMigrating()
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
            ParentIssuesLastUpdate = default,
            Tags = [],
            TagsLastUpdate = default,
            Questions = [],
            QuestionsLastUpdate = default,
            ExecutionMode = ExecutionMode.Series,
            ExecutionModeLastUpdate = default,
            LastUpdate = timestamp,
            CreatedAt = default
        };
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic()]);

        Issue? savedIssue = null;
        await _storage.SaveIssuesAsync(
            Arg.Do<IReadOnlyList<Issue>>(issues => savedIssue = issues[0]),
            Arg.Any<CancellationToken>());

        await _sut.MigrateAsync();

        savedIssue.Should().NotBeNull();
        savedIssue!.TitleLastUpdate.Should().Be(timestamp);
        savedIssue.StatusLastUpdate.Should().Be(timestamp);
        savedIssue.TypeLastUpdate.Should().Be(timestamp);
        savedIssue.DescriptionLastUpdate.Should().Be(timestamp);
        savedIssue.CreatedAt.Should().Be(timestamp);
    }

    #endregion

    #region IsMigrationNeededAsync Tests

    [Test]
    public async Task IsMigrationNeededAsync_ReturnsTrue_WhenIssuesNeedTimestampMigration()
    {
        var issue = CreateIssueNeedingMigration();
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic()]);

        var result = await _sut.IsMigrationNeededAsync();

        result.Should().BeTrue();
    }

    [Test]
    public async Task IsMigrationNeededAsync_ReturnsTrue_WhenUnknownPropertiesExist()
    {
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();
        var unknownProps = new HashSet<string> { "unknownField" };
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic(unknownProperties: unknownProps)]);

        var result = await _sut.IsMigrationNeededAsync();

        result.Should().BeTrue();
    }

    [Test]
    public async Task IsMigrationNeededAsync_ReturnsFalse_WhenNothingNeeded()
    {
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic()]);

        var result = await _sut.IsMigrationNeededAsync();

        result.Should().BeFalse();
    }

    [Test]
    public async Task IsMigrationNeededAsync_ReturnsTrue_WhenBothTimestampsAndUnknownProperties()
    {
        var issue = CreateIssueNeedingMigration();
        var unknownProps = new HashSet<string> { "unknownField" };
        SetupLoadResult(
            issues: [issue],
            diagnostics: [CreateDiagnostic(unknownProperties: unknownProps)]);

        var result = await _sut.IsMigrationNeededAsync();

        result.Should().BeTrue();
    }

    #endregion
}
