using System.Security.Cryptography;
using System.Text;
using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;
using System.IO.Abstractions;

namespace Fleece.Core.Services;

internal sealed class JsonlStorageService : IStorageService
{
    private const string FleeceDirectory = ".fleece";
    private const string IssuesFileName = "issues.jsonl";
    private const string IssuesFilePattern = "issues*.jsonl";
    private const string TombstonesFileName = "tombstones.jsonl";
    private const string TombstonesFilePattern = "tombstones*.jsonl";
    private const int HashLength = 6;

    private readonly string _basePath;
    private readonly IJsonlSerializer _serializer;
    private readonly ISchemaValidator _schemaValidator;
    private readonly IFileSystem _fileSystem;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonlStorageService(
        string basePath,
        IJsonlSerializer serializer,
        ISchemaValidator schemaValidator,
        IFileSystem? fileSystem = null)
    {
        _basePath = basePath;
        _serializer = serializer;
        _schemaValidator = schemaValidator;
        _fileSystem = fileSystem ?? new Testably.Abstractions.RealFileSystem();
    }

    private string FleeceDirectoryPath => _fileSystem.Path.Combine(_basePath, FleeceDirectory);
    private string IssuesFilePath => _fileSystem.Path.Combine(FleeceDirectoryPath, IssuesFileName);
    private string TombstonesFilePath => _fileSystem.Path.Combine(FleeceDirectoryPath, TombstonesFileName);

    public async Task EnsureDirectoryExistsAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() => _fileSystem.Directory.CreateDirectory(FleeceDirectoryPath), cancellationToken);
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
                var content = await _fileSystem.File.ReadAllTextAsync(file, cancellationToken);
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
            var filePath = _fileSystem.Path.Combine(FleeceDirectoryPath, fileName);

            // Delete old issue files before writing new one
            var existingFiles = GetAllIssueFilesInternal();
            foreach (var file in existingFiles)
            {
                _fileSystem.File.Delete(file);
            }

            await _fileSystem.File.WriteAllLinesAsync(filePath, lines, cancellationToken);
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
                var content = await _fileSystem.File.ReadAllTextAsync(file, cancellationToken);
                var issues = _serializer.DeserializeIssues(content);
                existingIssues.AddRange(issues);
            }

            existingIssues.Add(issue);

            var lines = existingIssues.Select(_serializer.SerializeIssue).ToList();
            var combinedContent = string.Join(Environment.NewLine, lines);
            var hash = ComputeContentHash(combinedContent);
            var fileName = $"issues_{hash}.jsonl";
            var filePath = _fileSystem.Path.Combine(FleeceDirectoryPath, fileName);

            // Delete old issue files before writing new one
            foreach (var file in existingFiles)
            {
                _fileSystem.File.Delete(file);
            }

            await _fileSystem.File.WriteAllLinesAsync(filePath, lines, cancellationToken);
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
            if (!_fileSystem.File.Exists(filePath))
            {
                return [];
            }

            var content = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken);
            return _serializer.DeserializeIssues(content);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task DeleteIssueFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (_fileSystem.File.Exists(filePath))
        {
            _fileSystem.File.Delete(filePath);
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
            var filePath = _fileSystem.Path.Combine(FleeceDirectoryPath, fileName);

            await _fileSystem.File.WriteAllLinesAsync(filePath, lines, cancellationToken);
            return filePath;
        }
        finally
        {
            _lock.Release();
        }
    }

    private IReadOnlyList<string> GetAllIssueFilesInternal()
    {
        if (!_fileSystem.Directory.Exists(FleeceDirectoryPath))
        {
            return [];
        }

        // Get all files matching issues*.jsonl pattern
        var files = _fileSystem.Directory.GetFiles(FleeceDirectoryPath, IssuesFilePattern);

        // Also check for legacy issues.jsonl (without hash)
        if (files.Length == 0 && _fileSystem.File.Exists(IssuesFilePath))
        {
            return [IssuesFilePath];
        }

        return files;
    }

    public async Task<IReadOnlyList<Tombstone>> LoadTombstonesAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var allTombstones = new List<Tombstone>();
            var files = GetAllTombstoneFilesInternal();

            foreach (var file in files)
            {
                var content = await _fileSystem.File.ReadAllTextAsync(file, cancellationToken);
                var tombstones = _serializer.DeserializeTombstones(content);
                allTombstones.AddRange(tombstones);
            }

            // Deduplicate by IssueId, keeping newest CleanedAt
            return allTombstones
                .GroupBy(t => t.IssueId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(t => t.CleanedAt).First())
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveTombstonesAsync(IReadOnlyList<Tombstone> tombstones, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureDirectoryExistsAsync(cancellationToken);

            var hash = GetCurrentIssuesHashInternal();
            var fileName = string.IsNullOrEmpty(hash) ? TombstonesFileName : $"tombstones_{hash}.jsonl";
            var filePath = _fileSystem.Path.Combine(FleeceDirectoryPath, fileName);

            // Delete old tombstone files before writing new one
            var existingFiles = GetAllTombstoneFilesInternal();
            foreach (var file in existingFiles)
            {
                _fileSystem.File.Delete(file);
            }

            var lines = tombstones.Select(_serializer.SerializeTombstone);
            await _fileSystem.File.WriteAllLinesAsync(filePath, lines, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AppendTombstonesAsync(IReadOnlyList<Tombstone> tombstones, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureDirectoryExistsAsync(cancellationToken);

            // Load existing tombstones, add new ones, and save
            var existingTombstones = new List<Tombstone>();
            var existingFiles = GetAllTombstoneFilesInternal();

            foreach (var file in existingFiles)
            {
                var content = await _fileSystem.File.ReadAllTextAsync(file, cancellationToken);
                var existing = _serializer.DeserializeTombstones(content);
                existingTombstones.AddRange(existing);
            }

            existingTombstones.AddRange(tombstones);

            var hash = GetCurrentIssuesHashInternal();
            var fileName = string.IsNullOrEmpty(hash) ? TombstonesFileName : $"tombstones_{hash}.jsonl";
            var filePath = _fileSystem.Path.Combine(FleeceDirectoryPath, fileName);

            // Delete old tombstone files before writing new one
            foreach (var file in existingFiles)
            {
                _fileSystem.File.Delete(file);
            }

            var lines = existingTombstones.Select(_serializer.SerializeTombstone);
            await _fileSystem.File.WriteAllLinesAsync(filePath, lines, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<IReadOnlyList<string>> GetAllTombstoneFilesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(GetAllTombstoneFilesInternal());
    }

    private IReadOnlyList<string> GetAllTombstoneFilesInternal()
    {
        if (!_fileSystem.Directory.Exists(FleeceDirectoryPath))
        {
            return [];
        }

        var files = _fileSystem.Directory.GetFiles(FleeceDirectoryPath, TombstonesFilePattern);

        if (files.Length == 0 && _fileSystem.File.Exists(TombstonesFilePath))
        {
            return [TombstonesFilePath];
        }

        return files;
    }

    private string? GetCurrentIssuesHashInternal()
    {
        var issueFiles = GetAllIssueFilesInternal();
        if (issueFiles.Count == 0)
        {
            return null;
        }

        // Extract hash from first issues file (e.g., "issues_abc123.jsonl" -> "abc123")
        var fileName = _fileSystem.Path.GetFileNameWithoutExtension(issueFiles[0]);
        if (fileName.StartsWith("issues_") && fileName.Length > 7)
        {
            return fileName[7..];
        }

        return null;
    }

    public Task<(bool HasMultiple, string Message)> HasMultipleUnmergedFilesAsync(CancellationToken cancellationToken = default)
    {
        var issueFiles = GetAllIssueFilesInternal();
        var tombstoneFiles = GetAllTombstoneFilesInternal();

        var messages = new List<string>();

        if (issueFiles.Count > 1)
        {
            var fileNames = issueFiles.Select(_fileSystem.Path.GetFileName);
            messages.Add($"Multiple unmerged issue files found: {string.Join(", ", fileNames)}");
        }

        if (tombstoneFiles.Count > 1)
        {
            var fileNames = tombstoneFiles.Select(_fileSystem.Path.GetFileName);
            messages.Add($"Multiple unmerged tombstone files found: {string.Join(", ", fileNames)}");
        }

        if (messages.Count > 0)
        {
            var message = string.Join("\n", messages) + "\nRun 'fleece merge' to consolidate before continuing.";
            return Task.FromResult((true, message));
        }

        return Task.FromResult((false, string.Empty));
    }

    public async Task<LoadIssuesResult> LoadIssuesWithDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var allIssues = new List<Issue>();
            var diagnostics = new List<ParseDiagnostic>();
            var files = GetAllIssueFilesInternal();

            foreach (var file in files)
            {
                var content = await _fileSystem.File.ReadAllTextAsync(file, cancellationToken);

                // Validate schema and collect diagnostics
                var diagnostic = _schemaValidator.ValidateJsonlContent(file, content);
                diagnostics.Add(diagnostic);

                // Still try to load issues even if there are warnings
                var issues = _serializer.DeserializeIssues(content);
                allIssues.AddRange(issues);
            }

            // Deduplicate by ID, keeping the newest version
            var deduplicatedIssues = allIssues
                .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(i => i.LastUpdate).First())
                .ToList();

            return new LoadIssuesResult
            {
                Issues = deduplicatedIssues,
                Diagnostics = diagnostics
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return hashString[..HashLength];
    }
}
