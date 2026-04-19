using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;
using System.IO.Abstractions;

namespace Fleece.Core.Services;

/// <summary>
/// Storage service that operates on a single specified JSONL file.
/// Used when the --issues/-i option is provided to work with a custom file.
/// </summary>
internal sealed partial class SingleFileStorageService : IStorageService
{
    private const int HashLength = 6;

    private readonly string _filePath;
    private readonly bool _useHashNaming;
    private readonly IJsonlSerializer _serializer;
    private readonly ISchemaValidator _schemaValidator;
    private readonly IFileSystem _fileSystem;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Regex pattern to match standard issue filenames: issues_{6 hex chars}.jsonl
    /// </summary>
    [GeneratedRegex(@"^issues_[a-f0-9]{6}\.jsonl$", RegexOptions.IgnoreCase)]
    private static partial Regex StandardIssueFilePattern();

    public SingleFileStorageService(
        string filePath,
        IJsonlSerializer serializer,
        ISchemaValidator schemaValidator,
        IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new Testably.Abstractions.RealFileSystem();
        _filePath = _fileSystem.Path.GetFullPath(filePath);
        _serializer = serializer;
        _schemaValidator = schemaValidator;

        // Determine if this file follows the standard naming pattern
        var fileName = _fileSystem.Path.GetFileName(filePath);
        _useHashNaming = StandardIssueFilePattern().IsMatch(fileName);
    }

    public async Task<IReadOnlyList<Issue>> LoadIssuesAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_fileSystem.File.Exists(_filePath))
            {
                return [];
            }

            var content = await _fileSystem.File.ReadAllTextAsync(_filePath, cancellationToken);
            return _serializer.DeserializeIssues(content);
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
            var directory = _fileSystem.Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                _fileSystem.Directory.CreateDirectory(directory);
            }

            var lines = issues.Select(_serializer.SerializeIssue).ToList();

            if (_useHashNaming)
            {
                // Compute hash and write to new file with hash-based name
                var content = string.Join(Environment.NewLine, lines);
                var hash = ComputeContentHash(content);
                var newFileName = $"issues_{hash}.jsonl";
                var newFilePath = _fileSystem.Path.Combine(directory ?? ".", newFileName);

                // Delete old file if different from new path
                if (_fileSystem.File.Exists(_filePath) && !string.Equals(_filePath, newFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    _fileSystem.File.Delete(_filePath);
                }

                await _fileSystem.File.WriteAllLinesAsync(newFilePath, lines, cancellationToken);
            }
            else
            {
                // For non-standard filenames, simply overwrite the file
                await _fileSystem.File.WriteAllLinesAsync(_filePath, lines, cancellationToken);
            }
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
            var directory = _fileSystem.Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                _fileSystem.Directory.CreateDirectory(directory);
            }

            // Load existing issues and add new one
            var existingIssues = new List<Issue>();
            if (_fileSystem.File.Exists(_filePath))
            {
                var content = await _fileSystem.File.ReadAllTextAsync(_filePath, cancellationToken);
                existingIssues.AddRange(_serializer.DeserializeIssues(content));
            }

            existingIssues.Add(issue);
            var lines = existingIssues.Select(_serializer.SerializeIssue).ToList();

            if (_useHashNaming)
            {
                var combinedContent = string.Join(Environment.NewLine, lines);
                var hash = ComputeContentHash(combinedContent);
                var newFileName = $"issues_{hash}.jsonl";
                var newFilePath = _fileSystem.Path.Combine(directory ?? ".", newFileName);

                // Delete old file if different from new path
                if (_fileSystem.File.Exists(_filePath) && !string.Equals(_filePath, newFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    _fileSystem.File.Delete(_filePath);
                }

                await _fileSystem.File.WriteAllLinesAsync(newFilePath, lines, cancellationToken);
            }
            else
            {
                // For non-standard filenames, simply overwrite the file
                await _fileSystem.File.WriteAllLinesAsync(_filePath, lines, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task EnsureDirectoryExistsAsync(CancellationToken cancellationToken = default)
    {
        var directory = _fileSystem.Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetAllIssueFilesAsync(CancellationToken cancellationToken = default)
    {
        // For single file operation, return only this file if it exists
        if (_fileSystem.File.Exists(_filePath))
        {
            return Task.FromResult<IReadOnlyList<string>>(new[] { _filePath });
        }
        return Task.FromResult<IReadOnlyList<string>>([]);
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
            var directory = _fileSystem.Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                _fileSystem.Directory.CreateDirectory(directory);
            }

            var lines = issues.Select(_serializer.SerializeIssue).ToList();
            var content = string.Join(Environment.NewLine, lines);
            var hash = ComputeContentHash(content);
            var fileName = $"issues_{hash}.jsonl";
            var filePath = _fileSystem.Path.Combine(directory ?? ".", fileName);

            await _fileSystem.File.WriteAllLinesAsync(filePath, lines, cancellationToken);
            return filePath;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<(bool HasMultiple, string Message)> HasMultipleUnmergedFilesAsync(CancellationToken cancellationToken = default)
    {
        // Single file mode never has multiple unmerged files
        return Task.FromResult((false, string.Empty));
    }

    public async Task<LoadIssuesResult> LoadIssuesWithDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_fileSystem.File.Exists(_filePath))
            {
                return new LoadIssuesResult
                {
                    Issues = [],
                    Diagnostics = []
                };
            }

            var content = await _fileSystem.File.ReadAllTextAsync(_filePath, cancellationToken);
            var diagnostic = _schemaValidator.ValidateJsonlContent(_filePath, content);
            var issues = _serializer.DeserializeIssues(content);

            return new LoadIssuesResult
            {
                Issues = issues.ToList(),
                Diagnostics = [diagnostic]
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<IReadOnlyList<Tombstone>> LoadTombstonesAsync(CancellationToken cancellationToken = default)
    {
        // Single file mode does not support tombstones
        return Task.FromResult<IReadOnlyList<Tombstone>>([]);
    }

    public Task SaveTombstonesAsync(IReadOnlyList<Tombstone> tombstones, CancellationToken cancellationToken = default)
    {
        // Single file mode does not support tombstones - no-op
        return Task.CompletedTask;
    }

    public Task AppendTombstonesAsync(IReadOnlyList<Tombstone> tombstones, CancellationToken cancellationToken = default)
    {
        // Single file mode does not support tombstones - no-op
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetAllTombstoneFilesAsync(CancellationToken cancellationToken = default)
    {
        // Single file mode does not support tombstones
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return hashString[..HashLength];
    }
}
