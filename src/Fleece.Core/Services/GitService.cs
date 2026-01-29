using System.Diagnostics;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Service for performing git operations on the .fleece directory.
/// </summary>
public sealed class GitService : IGitService
{
    private readonly string _workingDirectory;

    /// <summary>
    /// Creates a new GitService instance.
    /// </summary>
    /// <param name="workingDirectory">The working directory for git operations. Defaults to current directory.</param>
    public GitService(string? workingDirectory = null)
    {
        _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public bool IsGitRepository()
    {
        var (exitCode, _, _) = RunGit("rev-parse --is-inside-work-tree");
        return exitCode == 0;
    }

    /// <inheritdoc />
    public bool HasFleeceChanges()
    {
        // Check for any changes (staged or unstaged) in .fleece directory
        var (exitCode, output, _) = RunGit("status --porcelain .fleece/");
        return exitCode == 0 && !string.IsNullOrWhiteSpace(output);
    }

    /// <inheritdoc />
    public GitOperationResult StageFleeceDirectory()
    {
        var (exitCode, _, error) = RunGit("add .fleece/");
        return exitCode == 0
            ? GitOperationResult.Ok()
            : GitOperationResult.Fail(error);
    }

    /// <inheritdoc />
    public GitOperationResult Commit(string message)
    {
        // Escape quotes in message
        var escapedMessage = message.Replace("\"", "\\\"");
        var (exitCode, output, error) = RunGit($"commit -m \"{escapedMessage}\"");

        if (exitCode != 0)
        {
            // Check both stdout and stderr for common messages
            var combinedOutput = $"{output} {error}".ToLowerInvariant();
            if (combinedOutput.Contains("nothing to commit") || combinedOutput.Contains("no changes added"))
            {
                return GitOperationResult.Fail("nothing to commit");
            }
            return GitOperationResult.Fail(string.IsNullOrWhiteSpace(error) ? output : error);
        }

        return GitOperationResult.Ok();
    }

    /// <inheritdoc />
    public GitOperationResult Push()
    {
        var (exitCode, _, error) = RunGit("push");
        return exitCode == 0
            ? GitOperationResult.Ok()
            : GitOperationResult.Fail(error);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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
