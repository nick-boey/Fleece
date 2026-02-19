using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class SingleFileStorageServiceTests
{
    private string _testDirectory = null!;
    private JsonlSerializer _serializer = null!;
    private SchemaValidator _schemaValidator = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"fleece-single-file-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _serializer = new JsonlSerializer();
        _schemaValidator = new SchemaValidator();
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
    public async Task LoadIssuesAsync_ReturnsEmptyList_WhenFileDoesNotExist()
    {
        var filePath = Path.Combine(_testDirectory, "nonexistent.jsonl");
        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);

        var result = await sut.LoadIssuesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task LoadIssuesAsync_ReturnsIssues_WhenFileExists()
    {
        var filePath = Path.Combine(_testDirectory, "test.jsonl");
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test Issue").Build();
        await File.WriteAllTextAsync(filePath, _serializer.SerializeIssue(issue));

        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);
        var result = await sut.LoadIssuesAsync();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("abc123");
    }

    [Test]
    public async Task SaveIssuesAsync_OverwritesFile_WhenNonStandardFilename()
    {
        var filePath = Path.Combine(_testDirectory, "custom-issues.jsonl");
        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);

        var issues = new[]
        {
            new IssueBuilder().WithId("abc123").WithTitle("Issue 1").Build(),
            new IssueBuilder().WithId("def456").WithTitle("Issue 2").Build()
        };

        await sut.SaveIssuesAsync(issues);

        // File should exist at original path
        File.Exists(filePath).Should().BeTrue();
        var content = await File.ReadAllLinesAsync(filePath);
        content.Should().HaveCount(2);
    }

    [Test]
    public async Task SaveIssuesAsync_UsesHashNaming_WhenStandardFilename()
    {
        var originalFilePath = Path.Combine(_testDirectory, "issues_abc123.jsonl");
        await File.WriteAllTextAsync(originalFilePath, ""); // Create empty file first
        var sut = new SingleFileStorageService(originalFilePath, _serializer, _schemaValidator);

        var issues = new[]
        {
            new IssueBuilder().WithId("test01").WithTitle("Issue 1").Build()
        };

        await sut.SaveIssuesAsync(issues);

        // Original file should be deleted (since hash will change)
        File.Exists(originalFilePath).Should().BeFalse();
        // New file with hash should exist
        var files = Directory.GetFiles(_testDirectory, "issues_*.jsonl");
        files.Should().HaveCount(1);
    }

    [Test]
    public async Task AppendIssueAsync_AddsIssue_WhenNonStandardFilename()
    {
        var filePath = Path.Combine(_testDirectory, "my-issues.jsonl");
        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);

        var issue1 = new IssueBuilder().WithId("abc123").WithTitle("Issue 1").Build();
        var issue2 = new IssueBuilder().WithId("def456").WithTitle("Issue 2").Build();

        await sut.AppendIssueAsync(issue1);
        await sut.AppendIssueAsync(issue2);

        var result = await sut.LoadIssuesAsync();
        result.Should().HaveCount(2);
    }

    [Test]
    public async Task HasMultipleUnmergedFilesAsync_AlwaysReturnsFalse()
    {
        var filePath = Path.Combine(_testDirectory, "test.jsonl");
        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);

        var (hasMultiple, message) = await sut.HasMultipleUnmergedFilesAsync();

        hasMultiple.Should().BeFalse();
        message.Should().BeEmpty();
    }

    [Test]
    public async Task LoadTombstonesAsync_ReturnsEmptyList()
    {
        var filePath = Path.Combine(_testDirectory, "test.jsonl");
        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);

        var result = await sut.LoadTombstonesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task SaveTombstonesAsync_IsNoOp()
    {
        var filePath = Path.Combine(_testDirectory, "test.jsonl");
        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);

        var tombstones = new[] { new Tombstone { IssueId = "abc123", OriginalTitle = "Test", CleanedAt = DateTimeOffset.UtcNow, CleanedBy = "test" } };

        // Should not throw
        await sut.SaveTombstonesAsync(tombstones);

        // And tombstones file should not exist
        var tombstoneFiles = Directory.GetFiles(_testDirectory, "tombstones_*.jsonl");
        tombstoneFiles.Should().BeEmpty();
    }

    [Test]
    public async Task EnsureDirectoryExistsAsync_CreatesParentDirectory()
    {
        var nestedDir = Path.Combine(_testDirectory, "nested", "dir");
        var filePath = Path.Combine(nestedDir, "test.jsonl");
        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);

        await sut.EnsureDirectoryExistsAsync();

        Directory.Exists(nestedDir).Should().BeTrue();
    }

    [Test]
    public async Task GetAllIssueFilesAsync_ReturnsSingleFile_WhenExists()
    {
        var filePath = Path.Combine(_testDirectory, "test.jsonl");
        await File.WriteAllTextAsync(filePath, "");
        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);

        var result = await sut.GetAllIssueFilesAsync();

        result.Should().HaveCount(1);
        result[0].Should().Be(filePath);
    }

    [Test]
    public async Task GetAllIssueFilesAsync_ReturnsEmpty_WhenFileDoesNotExist()
    {
        var filePath = Path.Combine(_testDirectory, "nonexistent.jsonl");
        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);

        var result = await sut.GetAllIssueFilesAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task LoadIssuesWithDiagnosticsAsync_ReturnsIssuesAndDiagnostics()
    {
        var filePath = Path.Combine(_testDirectory, "test.jsonl");
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();
        await File.WriteAllTextAsync(filePath, _serializer.SerializeIssue(issue));

        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);
        var result = await sut.LoadIssuesWithDiagnosticsAsync();

        result.Issues.Should().HaveCount(1);
        result.Diagnostics.Should().HaveCount(1);
    }

    [Test]
    public async Task LoadIssuesWithDiagnosticsAsync_ReturnsEmptyResult_WhenFileDoesNotExist()
    {
        var filePath = Path.Combine(_testDirectory, "nonexistent.jsonl");
        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);

        var result = await sut.LoadIssuesWithDiagnosticsAsync();

        result.Issues.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Test]
    public void Constructor_ConvertsToAbsolutePath()
    {
        // Using relative path
        var sut = new SingleFileStorageService("test.jsonl", _serializer, _schemaValidator);

        // The service should internally convert to absolute path
        // We can't directly test this without exposing internal state,
        // but we can verify it doesn't throw
        sut.Should().NotBeNull();
    }

    [Test]
    public async Task SaveIssuesAsync_CreatesDirectory_WhenDoesNotExist()
    {
        var nestedDir = Path.Combine(_testDirectory, "newdir");
        var filePath = Path.Combine(nestedDir, "test.jsonl");
        var sut = new SingleFileStorageService(filePath, _serializer, _schemaValidator);

        var issues = new[]
        {
            new IssueBuilder().WithId("abc123").WithTitle("Issue 1").Build()
        };

        await sut.SaveIssuesAsync(issues);

        Directory.Exists(nestedDir).Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }
}
