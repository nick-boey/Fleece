using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class JsonlStorageServiceTests
{
    private string _testDirectory = null!;
    private JsonlStorageService _sut = null!;
    private JsonlSerializer _serializer = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"fleece-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _serializer = new JsonlSerializer();
        _sut = new JsonlStorageService(_testDirectory, _serializer);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Test]
    public async Task EnsureDirectoryExistsAsync_CreatesFleeceDirectory()
    {
        await _sut.EnsureDirectoryExistsAsync();

        Directory.Exists(Path.Combine(_testDirectory, ".fleece")).Should().BeTrue();
    }

    [Test]
    public async Task LoadIssuesAsync_ReturnsEmptyList_WhenFileDoesNotExist()
    {
        var result = await _sut.LoadIssuesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task SaveIssuesAsync_CreatesFileWithIssues()
    {
        var issues = new[]
        {
            new IssueBuilder().WithId("abc123").WithTitle("Issue 1").Build(),
            new IssueBuilder().WithId("def456").WithTitle("Issue 2").Build()
        };

        await _sut.SaveIssuesAsync(issues);

        var filePath = Path.Combine(_testDirectory, ".fleece", "issues.jsonl");
        File.Exists(filePath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(filePath);
        lines.Should().HaveCount(2);
    }

    [Test]
    public async Task LoadIssuesAsync_ReturnsIssues_AfterSave()
    {
        var issues = new[]
        {
            new IssueBuilder().WithId("abc123").WithTitle("Issue 1").Build(),
            new IssueBuilder().WithId("def456").WithTitle("Issue 2").Build()
        };
        await _sut.SaveIssuesAsync(issues);

        var result = await _sut.LoadIssuesAsync();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("abc123");
        result[1].Id.Should().Be("def456");
    }

    [Test]
    public async Task AppendIssueAsync_AddsIssueToFile()
    {
        var issue1 = new IssueBuilder().WithId("abc123").WithTitle("Issue 1").Build();
        var issue2 = new IssueBuilder().WithId("def456").WithTitle("Issue 2").Build();

        await _sut.AppendIssueAsync(issue1);
        await _sut.AppendIssueAsync(issue2);

        var result = await _sut.LoadIssuesAsync();
        result.Should().HaveCount(2);
    }

    [Test]
    public async Task LoadConflictsAsync_ReturnsEmptyList_WhenFileDoesNotExist()
    {
        var result = await _sut.LoadConflictsAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task SaveConflictsAsync_CreatesFileWithConflicts()
    {
        var older = new IssueBuilder().WithId("abc123").WithTitle("Old").Build();
        var newer = new IssueBuilder().WithId("abc123").WithTitle("New").Build();
        var conflicts = new[]
        {
            new ConflictRecord
            {
                ConflictId = Guid.NewGuid(),
                IssueId = "abc123",
                OlderVersion = older,
                NewerVersion = newer,
                DetectedAt = DateTimeOffset.UtcNow
            }
        };

        await _sut.SaveConflictsAsync(conflicts);

        var filePath = Path.Combine(_testDirectory, ".fleece", "conflicts.jsonl");
        File.Exists(filePath).Should().BeTrue();
    }

    [Test]
    public async Task LoadConflictsAsync_ReturnsConflicts_AfterSave()
    {
        var older = new IssueBuilder().WithId("abc123").WithTitle("Old").Build();
        var newer = new IssueBuilder().WithId("abc123").WithTitle("New").Build();
        var conflict = new ConflictRecord
        {
            ConflictId = Guid.NewGuid(),
            IssueId = "abc123",
            OlderVersion = older,
            NewerVersion = newer,
            DetectedAt = DateTimeOffset.UtcNow
        };
        await _sut.SaveConflictsAsync([conflict]);

        var result = await _sut.LoadConflictsAsync();

        result.Should().HaveCount(1);
        result[0].IssueId.Should().Be("abc123");
    }

    [Test]
    public async Task AppendConflictAsync_AddsConflictToFile()
    {
        var older = new IssueBuilder().WithId("abc123").WithTitle("Old").Build();
        var newer = new IssueBuilder().WithId("abc123").WithTitle("New").Build();
        var conflict = new ConflictRecord
        {
            ConflictId = Guid.NewGuid(),
            IssueId = "abc123",
            OlderVersion = older,
            NewerVersion = newer,
            DetectedAt = DateTimeOffset.UtcNow
        };

        await _sut.AppendConflictAsync(conflict);

        var result = await _sut.LoadConflictsAsync();
        result.Should().HaveCount(1);
    }

    [Test]
    public async Task SaveIssuesAsync_OverwritesExistingFile()
    {
        var original = new IssueBuilder().WithId("abc123").WithTitle("Original").Build();
        await _sut.SaveIssuesAsync([original]);

        var replacement = new IssueBuilder().WithId("def456").WithTitle("Replacement").Build();
        await _sut.SaveIssuesAsync([replacement]);

        var result = await _sut.LoadIssuesAsync();
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("def456");
    }
}
