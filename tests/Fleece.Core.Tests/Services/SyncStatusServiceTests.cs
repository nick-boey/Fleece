using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class SyncStatusServiceTests
{
    private string _testDirectory = null!;
    private IJsonlSerializer _serializer = null!;
    private IGitService _gitService = null!;
    private SyncStatusService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"fleece-sync-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _serializer = new JsonlSerializer();
        _gitService = Substitute.For<IGitService>();
        _sut = new SyncStatusService(_testDirectory, _serializer, _gitService);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDirectory))
        {
            foreach (var file in Directory.GetFiles(_testDirectory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    // === Tests for non-git repositories ===

    [Test]
    public async Task GetSyncStatusesAsync_ReturnsAllLocal_WhenGitNotAvailable()
    {
        // Arrange
        _gitService.IsGitAvailable().Returns(false);
        CreateIssueFile("issue1", "Test Issue 1");

        // Act
        var result = await _sut.GetSyncStatusesAsync();

        // Assert
        result.Should().ContainKey("issue1");
        result["issue1"].Should().Be(SyncStatus.Local);
    }

    [Test]
    public async Task GetSyncStatusesAsync_ReturnsAllLocal_WhenNotInGitRepository()
    {
        // Arrange
        _gitService.IsGitAvailable().Returns(true);
        _gitService.IsGitRepository().Returns(false);
        CreateIssueFile("issue1", "Test Issue 1");

        // Act
        var result = await _sut.GetSyncStatusesAsync();

        // Assert
        result.Should().ContainKey("issue1");
        result["issue1"].Should().Be(SyncStatus.Local);
    }

    [Test]
    public async Task GetSyncStatusesAsync_ReturnsEmptyDictionary_WhenNoIssues()
    {
        // Arrange
        _gitService.IsGitAvailable().Returns(false);

        // Act
        var result = await _sut.GetSyncStatusesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // === Tests using real git ===

    [Test]
    public async Task GetSyncStatusesAsync_ReturnsLocal_WhenIssueNotCommitted()
    {
        // Arrange
        InitGitRepo();
        CreateIssueFile("newissue", "New uncommitted issue");

        var realGitService = new GitService(_testDirectory);
        var sutWithRealGit = new SyncStatusService(_testDirectory, _serializer, realGitService);

        // Act
        var result = await sutWithRealGit.GetSyncStatusesAsync();

        // Assert
        result.Should().ContainKey("newissue");
        result["newissue"].Should().Be(SyncStatus.Local);
    }

    [Test]
    public async Task GetSyncStatusesAsync_ReturnsCommitted_WhenIssueCommittedButNoUpstream()
    {
        // Arrange
        InitGitRepo();
        CreateIssueFile("issue1", "Committed issue");
        RunGit("add .fleece/");
        RunGit("commit -m \"Add issue\"");

        var realGitService = new GitService(_testDirectory);
        var sutWithRealGit = new SyncStatusService(_testDirectory, _serializer, realGitService);

        // Act
        var result = await sutWithRealGit.GetSyncStatusesAsync();

        // Assert
        result.Should().ContainKey("issue1");
        result["issue1"].Should().Be(SyncStatus.Committed);
    }

    [Test]
    public async Task GetSyncStatusesAsync_ReturnsLocal_WhenIssueModifiedAfterCommit()
    {
        // Arrange
        InitGitRepo();
        CreateIssueFile("issue1", "Original title");
        RunGit("add .fleece/");
        RunGit("commit -m \"Add issue\"");

        // Modify the issue
        CreateIssueFile("issue1", "Modified title");

        var realGitService = new GitService(_testDirectory);
        var sutWithRealGit = new SyncStatusService(_testDirectory, _serializer, realGitService);

        // Act
        var result = await sutWithRealGit.GetSyncStatusesAsync();

        // Assert
        result.Should().ContainKey("issue1");
        result["issue1"].Should().Be(SyncStatus.Local);
    }

    [Test]
    public async Task GetSyncStatusesAsync_HandlesMultipleIssues()
    {
        // Arrange
        InitGitRepo();

        // Create and commit first issue
        CreateIssueFile("committed", "Committed issue");
        RunGit("add .fleece/");
        RunGit("commit -m \"Add committed issue\"");

        // Create new issue without committing
        CreateIssueFileAppend("newlocal", "New local issue");

        var realGitService = new GitService(_testDirectory);
        var sutWithRealGit = new SyncStatusService(_testDirectory, _serializer, realGitService);

        // Act
        var result = await sutWithRealGit.GetSyncStatusesAsync();

        // Assert
        result.Should().HaveCount(2);
        result["committed"].Should().Be(SyncStatus.Committed);
        result["newlocal"].Should().Be(SyncStatus.Local);
    }

    [Test]
    public async Task GetSyncStatusesAsync_HandlesMultipleIssueFiles()
    {
        // Arrange
        InitGitRepo();

        // Create issues in separate files
        CreateIssueFileWithName("issues_abc123.jsonl", "issue1", "Issue in file 1");
        CreateIssueFileWithName("issues_def456.jsonl", "issue2", "Issue in file 2");
        RunGit("add .fleece/");
        RunGit("commit -m \"Add issues\"");

        var realGitService = new GitService(_testDirectory);
        var sutWithRealGit = new SyncStatusService(_testDirectory, _serializer, realGitService);

        // Act
        var result = await sutWithRealGit.GetSyncStatusesAsync();

        // Assert
        result.Should().ContainKey("issue1");
        result.Should().ContainKey("issue2");
        result["issue1"].Should().Be(SyncStatus.Committed);
        result["issue2"].Should().Be(SyncStatus.Committed);
    }

    [Test]
    public async Task GetSyncStatusesAsync_IsCaseInsensitive()
    {
        // Arrange
        InitGitRepo();
        CreateIssueFile("AbCdEf", "Test issue with mixed case ID");
        RunGit("add .fleece/");
        RunGit("commit -m \"Add issue\"");

        var realGitService = new GitService(_testDirectory);
        var sutWithRealGit = new SyncStatusService(_testDirectory, _serializer, realGitService);

        // Act
        var result = await sutWithRealGit.GetSyncStatusesAsync();

        // Assert
        result.Should().ContainKey("AbCdEf");
        result.Should().ContainKey("abcdef");
        result.Should().ContainKey("ABCDEF");
    }

    // === Helper Methods ===

    private void InitGitRepo()
    {
        RunGit("init");
        RunGit("config user.email \"test@test.com\"");
        RunGit("config user.name \"Test User\"");
    }

    private void CreateIssueFile(string issueId, string title)
    {
        var fleeceDir = Path.Combine(_testDirectory, ".fleece");
        Directory.CreateDirectory(fleeceDir);

        var issue = new Issue
        {
            Id = issueId,
            Title = title,
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            LastUpdate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var json = _serializer.SerializeIssue(issue);
        File.WriteAllText(Path.Combine(fleeceDir, "issues_test.jsonl"), json);
    }

    private void CreateIssueFileAppend(string issueId, string title)
    {
        var fleeceDir = Path.Combine(_testDirectory, ".fleece");
        Directory.CreateDirectory(fleeceDir);

        var issue = new Issue
        {
            Id = issueId,
            Title = title,
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            LastUpdate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var json = _serializer.SerializeIssue(issue);
        var filePath = Path.Combine(fleeceDir, "issues_test.jsonl");
        File.AppendAllText(filePath, "\n" + json);
    }

    private void CreateIssueFileWithName(string fileName, string issueId, string title)
    {
        var fleeceDir = Path.Combine(_testDirectory, ".fleece");
        Directory.CreateDirectory(fleeceDir);

        var issue = new Issue
        {
            Id = issueId,
            Title = title,
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            LastUpdate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var json = _serializer.SerializeIssue(issue);
        File.WriteAllText(Path.Combine(fleeceDir, fileName), json);
    }

    private string RunGit(string arguments)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _testDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
