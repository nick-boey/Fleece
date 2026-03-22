using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class DiffServiceTests
{
    private IJsonlSerializer _serializer = null!;
    private DiffService _sut = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _serializer = new JsonlSerializer();
        _sut = new DiffService(_serializer);
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private async Task<string> CreateTempFileAsync(string name, IEnumerable<Issue> issues)
    {
        var path = Path.Combine(_tempDir, name);
        var content = string.Join("\n", issues.Select(i => _serializer.SerializeIssue(i)));
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    [Test]
    public async Task CompareFilesAsync_ReturnsEmpty_WhenFilesAreIdentical()
    {
        var issue = new IssueBuilder().WithId("abc123").WithTitle("Test").Build();
        var file1 = await CreateTempFileAsync("file1.jsonl", [issue]);
        var file2 = await CreateTempFileAsync("file2.jsonl", [issue]);

        var result = await _sut.CompareFilesAsync(file1, file2);

        result.HasDifferences.Should().BeFalse();
        result.Modified.Should().BeEmpty();
        result.OnlyInFile1.Should().BeEmpty();
        result.OnlyInFile2.Should().BeEmpty();
    }

    [Test]
    public async Task CompareFilesAsync_DetectsModifiedIssues()
    {
        var issue1 = new IssueBuilder().WithId("abc123").WithTitle("Original").WithStatus(IssueStatus.Open).Build();
        var issue2 = new IssueBuilder().WithId("abc123").WithTitle("Modified").WithStatus(IssueStatus.Progress).Build();

        var file1 = await CreateTempFileAsync("file1.jsonl", [issue1]);
        var file2 = await CreateTempFileAsync("file2.jsonl", [issue2]);

        var result = await _sut.CompareFilesAsync(file1, file2);

        result.HasDifferences.Should().BeTrue();
        result.Modified.Should().HaveCount(1);
        result.Modified[0].File1Version.Title.Should().Be("Original");
        result.Modified[0].File2Version.Title.Should().Be("Modified");
    }

    [Test]
    public async Task CompareFilesAsync_DetectsIssuesOnlyInFile1()
    {
        var sharedIssue = new IssueBuilder().WithId("shared").WithTitle("Shared").Build();
        var file1Only = new IssueBuilder().WithId("file1only").WithTitle("Only in file 1").Build();

        var file1 = await CreateTempFileAsync("file1.jsonl", [sharedIssue, file1Only]);
        var file2 = await CreateTempFileAsync("file2.jsonl", [sharedIssue]);

        var result = await _sut.CompareFilesAsync(file1, file2);

        result.HasDifferences.Should().BeTrue();
        result.OnlyInFile1.Should().HaveCount(1);
        result.OnlyInFile1[0].Id.Should().Be("file1only");
        result.OnlyInFile2.Should().BeEmpty();
    }

    [Test]
    public async Task CompareFilesAsync_DetectsIssuesOnlyInFile2()
    {
        var sharedIssue = new IssueBuilder().WithId("shared").WithTitle("Shared").Build();
        var file2Only = new IssueBuilder().WithId("file2only").WithTitle("Only in file 2").Build();

        var file1 = await CreateTempFileAsync("file1.jsonl", [sharedIssue]);
        var file2 = await CreateTempFileAsync("file2.jsonl", [sharedIssue, file2Only]);

        var result = await _sut.CompareFilesAsync(file1, file2);

        result.HasDifferences.Should().BeTrue();
        result.OnlyInFile2.Should().HaveCount(1);
        result.OnlyInFile2[0].Id.Should().Be("file2only");
        result.OnlyInFile1.Should().BeEmpty();
    }

    [Test]
    public async Task CompareFilesAsync_WithDuplicateIssueIds_DeduplicatesAndSucceeds()
    {
        var older = DateTimeOffset.UtcNow.AddHours(-1);
        var newer = DateTimeOffset.UtcNow;

        // File 1 has duplicate issue IDs - should use newer version
        var issue1Old = new IssueBuilder().WithId("abc123").WithTitle("Version1").WithLastUpdate(older).Build();
        var issue1New = new IssueBuilder().WithId("abc123").WithTitle("Version2").WithLastUpdate(newer).Build();

        // File 2 has a single issue with same ID but different content
        var issue2 = new IssueBuilder().WithId("abc123").WithTitle("Version3").WithLastUpdate(newer).Build();

        var file1 = await CreateTempFileAsync("file1.jsonl", [issue1Old, issue1New]);
        var file2 = await CreateTempFileAsync("file2.jsonl", [issue2]);

        var result = await _sut.CompareFilesAsync(file1, file2);

        // Should succeed without throwing ArgumentException
        result.Should().NotBeNull();
        result.Modified.Should().HaveCount(1);
        // File 1's deduplicated version (Version2) should be compared with File 2's version (Version3)
        result.Modified[0].File1Version.Title.Should().Be("Version2");
        result.Modified[0].File2Version.Title.Should().Be("Version3");
    }

    [Test]
    public async Task CompareFilesAsync_WithEmptyFiles_ReturnsNoResults()
    {
        var file1 = await CreateTempFileAsync("file1.jsonl", []);
        var file2 = await CreateTempFileAsync("file2.jsonl", []);

        var result = await _sut.CompareFilesAsync(file1, file2);

        result.HasDifferences.Should().BeFalse();
        result.TotalDifferences.Should().Be(0);
    }

    [Test]
    public async Task CompareFilesAsync_DetectsAllDifferenceTypes()
    {
        var shared = new IssueBuilder().WithId("shared").WithTitle("Shared").Build();
        var modified1 = new IssueBuilder().WithId("modified").WithTitle("Title1").Build();
        var modified2 = new IssueBuilder().WithId("modified").WithTitle("Title2").Build();
        var onlyIn1 = new IssueBuilder().WithId("only1").WithTitle("Only in 1").Build();
        var onlyIn2 = new IssueBuilder().WithId("only2").WithTitle("Only in 2").Build();

        var file1 = await CreateTempFileAsync("file1.jsonl", [shared, modified1, onlyIn1]);
        var file2 = await CreateTempFileAsync("file2.jsonl", [shared, modified2, onlyIn2]);

        var result = await _sut.CompareFilesAsync(file1, file2);

        result.HasDifferences.Should().BeTrue();
        result.Modified.Should().HaveCount(1);
        result.OnlyInFile1.Should().HaveCount(1);
        result.OnlyInFile2.Should().HaveCount(1);
        result.TotalDifferences.Should().Be(3);
    }

    [Test]
    public async Task CompareFilesAsync_ComparesStatusDifferences()
    {
        var issue1 = new IssueBuilder().WithId("abc123").WithTitle("Test").WithStatus(IssueStatus.Open).Build();
        var issue2 = new IssueBuilder().WithId("abc123").WithTitle("Test").WithStatus(IssueStatus.Complete).Build();

        var file1 = await CreateTempFileAsync("file1.jsonl", [issue1]);
        var file2 = await CreateTempFileAsync("file2.jsonl", [issue2]);

        var result = await _sut.CompareFilesAsync(file1, file2);

        result.Modified.Should().HaveCount(1);
        result.Modified[0].File1Version.Status.Should().Be(IssueStatus.Open);
        result.Modified[0].File2Version.Status.Should().Be(IssueStatus.Complete);
    }
}
