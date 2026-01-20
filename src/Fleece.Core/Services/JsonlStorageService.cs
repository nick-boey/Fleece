using System.Security.Cryptography;
using System.Text;
using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class JsonlStorageService : IStorageService
{
    private const string FleeceDirectory = ".fleece";
    private const string IssuesFileName = "issues.jsonl";
    private const string IssuesFilePattern = "issues*.jsonl";
    private const string ConflictsFileName = "conflicts.jsonl";
    private const int HashLength = 6;

    private readonly string _basePath;
    private readonly IJsonlSerializer _serializer;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonlStorageService(string basePath, IJsonlSerializer serializer)
    {
        _basePath = basePath;
        _serializer = serializer;
    }

    private string FleeceDirectoryPath => Path.Combine(_basePath, FleeceDirectory);
    private string IssuesFilePath => Path.Combine(FleeceDirectoryPath, IssuesFileName);
    private string ConflictsFilePath => Path.Combine(FleeceDirectoryPath, ConflictsFileName);

    public async Task EnsureDirectoryExistsAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Directory.CreateDirectory(FleeceDirectoryPath), cancellationToken);
    }

    public async Task<IReadOnlyList<Issue>> LoadIssuesAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var allIssues = new List<Issue>();
            var files = GetAllIssueFilesInternal();

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
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveIssuesAsync(IReadOnlyList<Issue> issues, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureDirectoryExistsAsync(cancellationToken);

            var lines = issues.Select(_serializer.SerializeIssue).ToList();
            var content = string.Join(Environment.NewLine, lines);
            var hash = ComputeContentHash(content);
            var fileName = $"issues_{hash}.jsonl";
            var filePath = Path.Combine(FleeceDirectoryPath, fileName);

            // Delete old issue files before writing new one
            var existingFiles = GetAllIssueFilesInternal();
            foreach (var file in existingFiles)
            {
                File.Delete(file);
            }

            await File.WriteAllLinesAsync(filePath, lines, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AppendIssueAsync(Issue issue, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureDirectoryExistsAsync(cancellationToken);

            // Load existing issues, add new one, and save with new hash
            var existingIssues = new List<Issue>();
            var existingFiles = GetAllIssueFilesInternal();

            foreach (var file in existingFiles)
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                var issues = _serializer.DeserializeIssues(content);
                existingIssues.AddRange(issues);
            }

            existingIssues.Add(issue);

            var lines = existingIssues.Select(_serializer.SerializeIssue).ToList();
            var combinedContent = string.Join(Environment.NewLine, lines);
            var hash = ComputeContentHash(combinedContent);
            var fileName = $"issues_{hash}.jsonl";
            var filePath = Path.Combine(FleeceDirectoryPath, fileName);

            // Delete old issue files before writing new one
            foreach (var file in existingFiles)
            {
                File.Delete(file);
            }

            await File.WriteAllLinesAsync(filePath, lines, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<ConflictRecord>> LoadConflictsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(ConflictsFilePath))
            {
                return [];
            }

            var content = await File.ReadAllTextAsync(ConflictsFilePath, cancellationToken);
            return _serializer.DeserializeConflicts(content);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveConflictsAsync(IReadOnlyList<ConflictRecord> conflicts, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureDirectoryExistsAsync(cancellationToken);

            var lines = conflicts.Select(_serializer.SerializeConflict);
            await File.WriteAllLinesAsync(ConflictsFilePath, lines, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AppendConflictAsync(ConflictRecord conflict, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureDirectoryExistsAsync(cancellationToken);

            var line = _serializer.SerializeConflict(conflict) + Environment.NewLine;
            await File.AppendAllTextAsync(ConflictsFilePath, line, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<IReadOnlyList<string>> GetAllIssueFilesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(GetAllIssueFilesInternal());
    }

    public async Task<IReadOnlyList<Issue>> LoadIssuesFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
            {
                return [];
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return _serializer.DeserializeIssues(content);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task DeleteIssueFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }

    public async Task<string> SaveIssuesWithHashAsync(IReadOnlyList<Issue> issues, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureDirectoryExistsAsync(cancellationToken);

            var lines = issues.Select(_serializer.SerializeIssue).ToList();
            var content = string.Join(Environment.NewLine, lines);
            var hash = ComputeContentHash(content);
            var fileName = $"issues_{hash}.jsonl";
            var filePath = Path.Combine(FleeceDirectoryPath, fileName);

            await File.WriteAllLinesAsync(filePath, lines, cancellationToken);
            return filePath;
        }
        finally
        {
            _lock.Release();
        }
    }

    private IReadOnlyList<string> GetAllIssueFilesInternal()
    {
        if (!Directory.Exists(FleeceDirectoryPath))
        {
            return [];
        }

        // Get all files matching issues*.jsonl pattern
        var files = Directory.GetFiles(FleeceDirectoryPath, IssuesFilePattern);

        // Also check for legacy issues.jsonl (without hash)
        if (files.Length == 0 && File.Exists(IssuesFilePath))
        {
            return [IssuesFilePath];
        }

        return files;
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return hashString[..HashLength];
    }
}
