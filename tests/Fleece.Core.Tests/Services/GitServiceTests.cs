using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class GitServiceTests
{
    private string _testDirectory = null!;
    private GitService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"fleece-git-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _sut = new GitService(_testDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDirectory))
        {
            // On Windows, git files can be read-only, so we need to reset attributes
            foreach (var file in Directory.GetFiles(_testDirectory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    // === IsGitAvailable Tests ===

    [Test]
    public void IsGitAvailable_ReturnsTrue_WhenGitInstalled()
    {
        // Git should be available in test environment
        _sut.IsGitAvailable().Should().BeTrue();
    }

    // === IsGitRepository Tests ===

    [Test]
    public void IsGitRepository_ReturnsFalse_WhenNotInRepo()
    {
        _sut.IsGitRepository().Should().BeFalse();
    }

    [Test]
    public void IsGitRepository_ReturnsTrue_WhenInRepo()
    {
        InitGitRepo();

        _sut.IsGitRepository().Should().BeTrue();
    }

    // === HasFleeceChanges Tests ===

    [Test]
    public void HasFleeceChanges_ReturnsFalse_WhenNoFleeceDirectory()
    {
        InitGitRepo();

        _sut.HasFleeceChanges().Should().BeFalse();
    }

    [Test]
    public void HasFleeceChanges_ReturnsTrue_WhenUntrackedFilesExist()
    {
        InitGitRepo();
        CreateFleeceFile("issues.jsonl", "{}");

        _sut.HasFleeceChanges().Should().BeTrue();
    }

    [Test]
    public void HasFleeceChanges_ReturnsFalse_WhenAllFilesCommitted()
    {
        InitGitRepo();
        CreateFleeceFile("issues.jsonl", "{}");
        RunGit("add .fleece/");
        RunGit("commit -m \"Add issues\"");

        _sut.HasFleeceChanges().Should().BeFalse();
    }

    [Test]
    public void HasFleeceChanges_ReturnsTrue_WhenStagedChangesExist()
    {
        InitGitRepo();
        CreateFleeceFile("issues.jsonl", "{}");
        RunGit("add .fleece/");

        _sut.HasFleeceChanges().Should().BeTrue();
    }

    [Test]
    public void HasFleeceChanges_ReturnsTrue_WhenModifiedFilesExist()
    {
        InitGitRepo();
        CreateFleeceFile("issues.jsonl", "{}");
        RunGit("add .fleece/");
        RunGit("commit -m \"Add issues\"");

        // Modify the file
        CreateFleeceFile("issues.jsonl", "{\"updated\": true}");

        _sut.HasFleeceChanges().Should().BeTrue();
    }

    // === StageFleeceDirectory Tests ===

    [Test]
    public void StageFleeceDirectory_ReturnsSuccess_WhenFilesExist()
    {
        InitGitRepo();
        CreateFleeceFile("issues.jsonl", "{}");

        var result = _sut.StageFleeceDirectory();

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Test]
    public void StageFleeceDirectory_ReturnsError_WhenNoFleeceDirectory()
    {
        InitGitRepo();

        var result = _sut.StageFleeceDirectory();

        // git add on non-existent path returns an error
        result.Success.Should().BeFalse();
    }

    [Test]
    public void StageFleeceDirectory_ReturnsError_WhenNotInRepo()
    {
        CreateFleeceFile("issues.jsonl", "{}");

        var result = _sut.StageFleeceDirectory();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // === Commit Tests ===

    [Test]
    public void Commit_ReturnsSuccess_WhenStagedChangesExist()
    {
        InitGitRepo();
        CreateFleeceFile("issues.jsonl", "{}");
        RunGit("add .fleece/");

        var result = _sut.Commit("Test commit");

        result.Success.Should().BeTrue();
    }

    [Test]
    public void Commit_ReturnsError_WhenNothingStaged()
    {
        InitGitRepo();

        var result = _sut.Commit("Empty commit");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("nothing to commit");
    }

    [Test]
    public void Commit_ReturnsError_WhenNotInRepo()
    {
        var result = _sut.Commit("Test commit");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Commit_HandlesQuotesInMessage()
    {
        InitGitRepo();
        CreateFleeceFile("issues.jsonl", "{}");
        RunGit("add .fleece/");

        var result = _sut.Commit("Test \"quoted\" commit");

        result.Success.Should().BeTrue();

        // Verify commit message
        var logOutput = RunGit("log --oneline -1");
        logOutput.Should().Contain("quoted");
    }

    // === Push Tests ===

    [Test]
    public void Push_ReturnsError_WhenNoRemoteConfigured()
    {
        InitGitRepo();
        CreateFleeceFile("issues.jsonl", "{}");
        RunGit("add .fleece/");
        RunGit("commit -m \"Test\"");

        var result = _sut.Push();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Push_ReturnsError_WhenNotInRepo()
    {
        var result = _sut.Push();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // === CommitFleeceChanges Tests ===

    [Test]
    public void CommitFleeceChanges_StagesAndCommits()
    {
        InitGitRepo();
        CreateFleeceFile("issues.jsonl", "{}");

        var result = _sut.CommitFleeceChanges("Add issues");

        result.Success.Should().BeTrue();

        // Verify commit was made
        var logOutput = RunGit("log --oneline -1");
        logOutput.Should().Contain("Add issues");
    }

    [Test]
    public void CommitFleeceChanges_ReturnsError_WhenNoChanges()
    {
        InitGitRepo();

        var result = _sut.CommitFleeceChanges("Empty commit");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No changes");
    }

    [Test]
    public void CommitFleeceChanges_CommitsOnlyFleeceDirectory()
    {
        InitGitRepo();
        CreateFleeceFile("issues.jsonl", "{}");

        // Create another file outside .fleece
        File.WriteAllText(Path.Combine(_testDirectory, "other.txt"), "other content");

        var result = _sut.CommitFleeceChanges("Add issues");

        result.Success.Should().BeTrue();

        // Verify other.txt is still untracked
        var statusOutput = RunGit("status --porcelain");
        statusOutput.Should().Contain("other.txt");
    }

    // === CommitAndPushFleeceChanges Tests ===

    [Test]
    public void CommitAndPushFleeceChanges_CommitsButFailsPush_WhenNoRemote()
    {
        InitGitRepo();
        CreateFleeceFile("issues.jsonl", "{}");

        var result = _sut.CommitAndPushFleeceChanges("Add issues");

        // Commit succeeds but push fails (no remote)
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("push failed");

        // But commit should have been made
        var logOutput = RunGit("log --oneline -1");
        logOutput.Should().Contain("Add issues");
    }

    [Test]
    public void CommitAndPushFleeceChanges_ReturnsError_WhenNoChanges()
    {
        InitGitRepo();

        var result = _sut.CommitAndPushFleeceChanges("Empty commit");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No changes");
    }

    // === Helper Methods ===

    private void InitGitRepo()
    {
        RunGit("init");
        RunGit("config user.email \"test@test.com\"");
        RunGit("config user.name \"Test User\"");
    }

    private void CreateFleeceFile(string filename, string content)
    {
        var fleeceDir = Path.Combine(_testDirectory, ".fleece");
        Directory.CreateDirectory(fleeceDir);
        File.WriteAllText(Path.Combine(fleeceDir, filename), content);
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
