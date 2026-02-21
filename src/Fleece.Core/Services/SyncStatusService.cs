using System.Diagnostics;
using System.Text.Json;
using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Service for determining the git sync status of issues by comparing
/// working directory, HEAD commit, and remote upstream.
/// </summary>
public sealed class SyncStatusService : ISyncStatusService
{
    private const string FleeceDirectory = ".fleece";
    private const string IssuesFilePattern = "issues*.jsonl";

    private readonly string _workingDirectory;
    private readonly IJsonlSerializer _serializer;
    private readonly IGitService _gitService;

    public SyncStatusService(string workingDirectory, IJsonlSerializer serializer, IGitService gitService)
    {
        _workingDirectory = workingDirectory;
        _serializer = serializer;
        _gitService = gitService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, SyncStatus>> GetSyncStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, SyncStatus>(StringComparer.OrdinalIgnoreCase);

        // If not in a git repository, all issues are local
        if (!_gitService.IsGitAvailable() || !_gitService.IsGitRepository())
        {
            var workingIssues = await LoadIssuesFromWorkingDirectoryAsync(cancellationToken);
            foreach (var issue in workingIssues)
            {
                result[issue.Id] = SyncStatus.Local;
            }
            return result;
        }

        // Load issues from all sources
        var workingIssuesDict = await LoadIssuesFromWorkingDirectoryAsDictAsync(cancellationToken);
        var headIssuesDict = await LoadIssuesFromGitRefAsync("HEAD", cancellationToken);
        var upstreamIssuesDict = await LoadIssuesFromUpstreamAsync(cancellationToken);

        // Determine sync status for each issue
        foreach (var (issueId, workingIssue) in workingIssuesDict)
        {
            var workingJson = SerializeForComparison(workingIssue);

            // Check against upstream first
            if (upstreamIssuesDict != null &&
                upstreamIssuesDict.TryGetValue(issueId, out var upstreamIssue))
            {
                var upstreamJson = SerializeForComparison(upstreamIssue);
                if (workingJson == upstreamJson)
                {
                    result[issueId] = SyncStatus.Synced;
                    continue;
                }
            }

            // Check against HEAD
            if (headIssuesDict.TryGetValue(issueId, out var headIssue))
            {
                var headJson = SerializeForComparison(headIssue);
                if (workingJson == headJson)
                {
                    // In HEAD but not same in upstream (or no upstream)
                    result[issueId] = SyncStatus.Committed;
                    continue;
                }
            }

            // Different from HEAD or new
            result[issueId] = SyncStatus.Local;
        }

        return result;
    }

    private async Task<IReadOnlyList<Issue>> LoadIssuesFromWorkingDirectoryAsync(
        CancellationToken cancellationToken)
    {
        var fleecePath = Path.Combine(_workingDirectory, FleeceDirectory);
        if (!Directory.Exists(fleecePath))
        {
            return [];
        }

        var allIssues = new List<Issue>();
        var files = Directory.GetFiles(fleecePath, IssuesFilePattern);

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var issues = _serializer.DeserializeIssues(content);
            allIssues.AddRange(issues);
        }

        // Deduplicate by ID, keeping the newest version
        return allIssues
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(i => i.LastUpdate).First())
            .ToList();
    }

    private async Task<Dictionary<string, Issue>> LoadIssuesFromWorkingDirectoryAsDictAsync(
        CancellationToken cancellationToken)
    {
        var issues = await LoadIssuesFromWorkingDirectoryAsync(cancellationToken);
        return issues.ToDictionary(i => i.Id, i => i, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, Issue>> LoadIssuesFromGitRefAsync(
        string gitRef, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Issue>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Get list of .fleece files at the git ref
            var (exitCode, output, _) = await RunGitAsync(
                $"ls-tree -r --name-only {gitRef} -- {FleeceDirectory}/",
                cancellationToken);

            if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return result;
            }

            var files = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(f => f.StartsWith($"{FleeceDirectory}/issues") && f.EndsWith(".jsonl"))
                .ToList();

            foreach (var file in files)
            {
                var (showExitCode, content, _) = await RunGitAsync(
                    $"show {gitRef}:{file}",
                    cancellationToken);

                if (showExitCode == 0 && !string.IsNullOrWhiteSpace(content))
                {
                    var issues = _serializer.DeserializeIssues(content);
                    foreach (var issue in issues)
                    {
                        // Keep newest version if duplicate
                        if (!result.TryGetValue(issue.Id, out var existing) ||
                            issue.LastUpdate > existing.LastUpdate)
                        {
                            result[issue.Id] = issue;
                        }
                    }
                }
            }
        }
        catch
        {
            // If git operations fail, return empty dictionary
        }

        return result;
    }

    private async Task<Dictionary<string, Issue>?> LoadIssuesFromUpstreamAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if there's an upstream tracking branch
            var (exitCode, upstream, _) = await RunGitAsync(
                "rev-parse --abbrev-ref @{u}",
                cancellationToken);

            if (exitCode != 0 || string.IsNullOrWhiteSpace(upstream))
            {
                return null; // No upstream configured
            }

            return await LoadIssuesFromGitRefAsync("@{u}", cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private string SerializeForComparison(Issue issue)
    {
        // Serialize to JSON for comparison
        // This ensures we compare the full issue content
        return JsonSerializer.Serialize(issue, FleeceJsonContext.Default.Issue);
    }

    private Task<(int ExitCode, string Output, string Error)> RunGitAsync(
        string arguments, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
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
        }, cancellationToken);
    }
}
