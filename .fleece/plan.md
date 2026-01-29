# Implementation Plan: Commit and Push Changes (Issue 8zKf9g)

## Overview

Add the ability to commit and push `.fleece/` folder contents to git via:
1. **Core Library API** - `IGitService` interface in `Fleece.Core` for programmatic use
2. **CLI Commands** - `fleece commit` command and `--commit`/`--push` flags on `fleece create`

## Architecture

The codebase follows a clean separation pattern:
- **Fleece.Core** - Business logic, services, interfaces (the API layer)
- **Fleece.Cli** - Thin CLI wrappers using Spectre.Console.Cli

Existing `GitConfigService` demonstrates the pattern for executing git commands via `System.Diagnostics.Process`.

## TDD Implementation Approach

We will follow strict Test-Driven Development:
1. **RED** - Write failing tests first
2. **GREEN** - Write minimal code to make tests pass
3. **REFACTOR** - Clean up while keeping tests green

### Implementation Order (TDD)

```
Phase 1: Core API (Tests First)
├── 1.1 Write IGitService interface (contract design)
├── 1.2 Write GitServiceTests (RED - all tests fail)
├── 1.3 Implement GitService (GREEN - tests pass)
├── 1.4 Refactor if needed
└── 1.5 Register in DI

Phase 2: CLI Commit Command (Tests First)
├── 2.1 Write CommitSettings
├── 2.2 Write CommitCommand integration tests (RED)
├── 2.3 Implement CommitCommand (GREEN)
└── 2.4 Register in Program.cs

Phase 3: Create Command Enhancement (Tests First)
├── 3.1 Update CreateSettings with --commit/--push
├── 3.2 Write CreateCommand git integration tests (RED)
├── 3.3 Update CreateCommand implementation (GREEN)
└── 3.4 Refactor
```

---

## Phase 1: Core Library API

### 1.1 Create `IGitService` Interface
**File:** `src/Fleece.Core/Services/Interfaces/IGitService.cs`

```csharp
namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Service for git operations on the .fleece directory.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Checks if git is available on the system.
    /// </summary>
    bool IsGitAvailable();

    /// <summary>
    /// Checks if the current directory is within a git repository.
    /// </summary>
    bool IsGitRepository();

    /// <summary>
    /// Checks if there are staged or unstaged changes in the .fleece directory.
    /// </summary>
    bool HasFleeceChanges();

    /// <summary>
    /// Stages all files in the .fleece directory.
    /// </summary>
    /// <returns>Result indicating success or failure with error message.</returns>
    GitOperationResult StageFleeceDirectory();

    /// <summary>
    /// Creates a commit with the staged changes.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    GitOperationResult Commit(string message);

    /// <summary>
    /// Pushes committed changes to the remote.
    /// </summary>
    /// <returns>Result indicating success or failure with error message.</returns>
    GitOperationResult Push();

    /// <summary>
    /// Convenience method: stages .fleece directory and commits with message.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    GitOperationResult CommitFleeceChanges(string message);

    /// <summary>
    /// Convenience method: stages, commits, and pushes .fleece directory changes.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    GitOperationResult CommitAndPushFleeceChanges(string message);
}

/// <summary>
/// Result of a git operation.
/// </summary>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="ErrorMessage">Error message if operation failed, null otherwise.</param>
public record GitOperationResult(bool Success, string? ErrorMessage = null)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static GitOperationResult Ok() => new(true);

    /// <summary>
    /// Creates a failed result with error message.
    /// </summary>
    public static GitOperationResult Fail(string error) => new(false, error);
}
```

### 1.2 Write Tests First (RED Phase)
**File:** `tests/Fleece.Core.Tests/Services/GitServiceTests.cs`

```csharp
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
    public void StageFleeceDirectory_ReturnsSuccess_WhenNoFleeceDirectory()
    {
        InitGitRepo();

        var result = _sut.StageFleeceDirectory();

        // git add on non-existent path is not an error
        result.Success.Should().BeTrue();
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
        result.ErrorMessage.Should().NotBeNullOrEmpty();

        // But commit should have been made
        var logOutput = RunGit("log --oneline -1");
        logOutput.Should().Contain("Add issues");
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
```

### 1.3 Implement `GitService` (GREEN Phase)
**File:** `src/Fleece.Core/Services/GitService.cs`

```csharp
using System.Diagnostics;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class GitService : IGitService
{
    private readonly string _workingDirectory;

    public GitService(string? workingDirectory = null)
    {
        _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
    }

    public bool IsGitAvailable()
    {
        try
        {
            var (exitCode, _, _) = RunGit("--version");
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public bool IsGitRepository()
    {
        var (exitCode, _, _) = RunGit("rev-parse --is-inside-work-tree");
        return exitCode == 0;
    }

    public bool HasFleeceChanges()
    {
        // Check for any changes (staged or unstaged) in .fleece directory
        var (exitCode, output, _) = RunGit("status --porcelain .fleece/");
        return exitCode == 0 && !string.IsNullOrWhiteSpace(output);
    }

    public GitOperationResult StageFleeceDirectory()
    {
        var (exitCode, _, error) = RunGit("add .fleece/");
        return exitCode == 0
            ? GitOperationResult.Ok()
            : GitOperationResult.Fail(error);
    }

    public GitOperationResult Commit(string message)
    {
        // Escape quotes in message
        var escapedMessage = message.Replace("\"", "\\\"");
        var (exitCode, _, error) = RunGit($"commit -m \"{escapedMessage}\"");

        if (exitCode != 0)
        {
            // Provide friendlier error message for common cases
            if (error.Contains("nothing to commit") || error.Contains("no changes added"))
            {
                return GitOperationResult.Fail("nothing to commit");
            }
            return GitOperationResult.Fail(error);
        }

        return GitOperationResult.Ok();
    }

    public GitOperationResult Push()
    {
        var (exitCode, _, error) = RunGit("push");
        return exitCode == 0
            ? GitOperationResult.Ok()
            : GitOperationResult.Fail(error);
    }

    public GitOperationResult CommitFleeceChanges(string message)
    {
        if (!HasFleeceChanges())
        {
            return GitOperationResult.Fail("No changes to commit in .fleece directory");
        }

        var stageResult = StageFleeceDirectory();
        if (!stageResult.Success)
        {
            return stageResult;
        }

        return Commit(message);
    }

    public GitOperationResult CommitAndPushFleeceChanges(string message)
    {
        var commitResult = CommitFleeceChanges(message);
        if (!commitResult.Success)
        {
            return commitResult;
        }

        var pushResult = Push();
        if (!pushResult.Success)
        {
            return GitOperationResult.Fail($"Committed successfully but push failed: {pushResult.ErrorMessage}");
        }

        return GitOperationResult.Ok();
    }

    private (int ExitCode, string Output, string Error) RunGit(string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output.Trim(), error.Trim());
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }
}
```

### 1.4 Register in DI
**File:** `src/Fleece.Core/Extensions/ServiceCollectionExtensions.cs`

Add to `AddFleeceCore()` method:
```csharp
services.AddSingleton<IGitService, GitService>();
```

---

## Phase 2: CLI Commit Command

### 2.1 Create `CommitSettings`
**File:** `src/Fleece.Cli/Settings/CommitSettings.cs`

```csharp
using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class CommitSettings : CommandSettings
{
    [CommandOption("-m|--message <MESSAGE>")]
    [Description("Commit message (default: 'Update fleece issues')")]
    public string? Message { get; init; }

    [CommandOption("--push")]
    [Description("Push to remote after committing")]
    public bool Push { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
```

### 2.2 Create `CommitCommand`
**File:** `src/Fleece.Cli/Commands/CommitCommand.cs`

```csharp
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class CommitCommand : Command<CommitSettings>
{
    private readonly IGitService _gitService;
    private const string DefaultCommitMessage = "Update fleece issues";

    public CommitCommand(IGitService gitService)
    {
        _gitService = gitService;
    }

    public override int Execute(CommandContext context, CommitSettings settings)
    {
        // Check git availability
        if (!_gitService.IsGitAvailable())
        {
            AnsiConsole.MarkupLine("[red]Error:[/] git command not found. Please ensure git is installed and available in PATH.");
            return 1;
        }

        // Check if in git repository
        if (!_gitService.IsGitRepository())
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Not a git repository. Please run this command from within a git repository.");
            return 1;
        }

        // Check for changes
        if (!_gitService.HasFleeceChanges())
        {
            AnsiConsole.MarkupLine("[yellow]No changes to commit in .fleece directory.[/]");
            return 0;
        }

        var message = settings.Message ?? DefaultCommitMessage;

        // Commit (and optionally push)
        var result = settings.Push
            ? _gitService.CommitAndPushFleeceChanges(message)
            : _gitService.CommitFleeceChanges(message);

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {result.ErrorMessage}");
            return 1;
        }

        // Success output
        if (settings.Json)
        {
            var jsonResult = new { committed = true, pushed = settings.Push, message };
            AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(jsonResult));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Committed:[/] {message}");
            if (settings.Push)
            {
                AnsiConsole.MarkupLine("[green]Pushed to remote[/]");
            }
        }

        return 0;
    }
}
```

### 2.3 Register Command in Program.cs
**File:** `src/Fleece.Cli/Program.cs`

Add to configuration:
```csharp
config.AddCommand<CommitCommand>("commit")
    .WithDescription("Commit fleece changes to git")
    .WithExample("commit")
    .WithExample("commit", "-m", "Add new issues")
    .WithExample("commit", "--push");
```

---

## Phase 3: Create Command Enhancement

### 3.1 Update `CreateSettings`
**File:** `src/Fleece.Cli/Settings/CreateSettings.cs`

Add properties:
```csharp
[CommandOption("--commit")]
[Description("Commit changes to git after creating issue")]
public bool Commit { get; init; }

[CommandOption("--push")]
[Description("Push to remote after committing (implies --commit)")]
public bool Push { get; init; }
```

### 3.2 Update `CreateCommand`
**File:** `src/Fleece.Cli/Commands/CreateCommand.cs`

Changes:
1. Inject `IGitService` via constructor
2. After successful issue creation, if `--commit` or `--push`:
   - Call `_gitService.CommitFleeceChanges()` or `_gitService.CommitAndPushFleeceChanges()`
3. Output git operation results

```csharp
// Add to constructor
private readonly IGitService _gitService;

public CreateCommand(IIssueService issueService, IStorageService storageService, IGitService gitService)
{
    _issueService = issueService;
    _storageService = storageService;
    _gitService = gitService;
}

// Add at end of ExecuteAsync, after successful creation:
if (settings.Commit || settings.Push)
{
    var commitMessage = $"Add issue: {issue.Title}";
    var gitResult = settings.Push
        ? _gitService.CommitAndPushFleeceChanges(commitMessage)
        : _gitService.CommitFleeceChanges(commitMessage);

    if (!gitResult.Success)
    {
        AnsiConsole.MarkupLine($"[yellow]Warning:[/] Issue created but git operation failed: {gitResult.ErrorMessage}");
    }
    else
    {
        AnsiConsole.MarkupLine("[dim]Changes committed to git[/]");
        if (settings.Push)
        {
            AnsiConsole.MarkupLine("[dim]Pushed to remote[/]");
        }
    }
}
```

---

## Files Summary

### New Files (in order of creation for TDD)
1. `src/Fleece.Core/Services/Interfaces/IGitService.cs` - Interface & GitOperationResult
2. `tests/Fleece.Core.Tests/Services/GitServiceTests.cs` - Tests (write first!)
3. `src/Fleece.Core/Services/GitService.cs` - Implementation
4. `src/Fleece.Cli/Settings/CommitSettings.cs` - Command settings
5. `src/Fleece.Cli/Commands/CommitCommand.cs` - Command implementation

### Modified Files
1. `src/Fleece.Core/Extensions/ServiceCollectionExtensions.cs` - Add DI registration
2. `src/Fleece.Cli/Settings/CreateSettings.cs` - Add --commit and --push flags
3. `src/Fleece.Cli/Commands/CreateCommand.cs` - Handle commit/push after create
4. `src/Fleece.Cli/Program.cs` - Register commit command

---

## Error Messages

Standard error messages:
- `"git command not found. Please ensure git is installed and available in PATH."`
- `"Not a git repository. Please run this command from within a git repository."`
- `"No changes to commit in .fleece directory."`
- `"nothing to commit"`
- `"Committed successfully but push failed: {error}"`

## Success Messages

- `"Committed: {message}"`
- `"Pushed to remote"`
- `"Changes committed to git"` (for create command)

---

## TDD Execution Checklist

- [ ] Create `IGitService.cs` interface
- [ ] Create `GitServiceTests.cs` with all test cases (RED - tests fail/don't compile)
- [ ] Create `GitService.cs` stub (RED - tests compile but fail)
- [ ] Implement `GitService.cs` methods one by one (GREEN - tests pass)
- [ ] Run all tests, ensure passing
- [ ] Add DI registration
- [ ] Create `CommitSettings.cs`
- [ ] Create `CommitCommand.cs`
- [ ] Register in `Program.cs`
- [ ] Update `CreateSettings.cs` with new flags
- [ ] Update `CreateCommand.cs` with git integration
- [ ] Manual testing of CLI commands
- [ ] Final test run
