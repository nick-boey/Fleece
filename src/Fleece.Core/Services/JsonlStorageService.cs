using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class JsonlStorageService : IStorageService
{
    private const string FleeceDirectory = ".fleece";
    private const string IssuesFileName = "issues.jsonl";
    private const string ConflictsFileName = "conflicts.jsonl";

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
            if (!File.Exists(IssuesFilePath))
            {
                return [];
            }

            var content = await File.ReadAllTextAsync(IssuesFilePath, cancellationToken);
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
            await EnsureDirectoryExistsAsync(cancellationToken);

            var lines = issues.Select(_serializer.SerializeIssue);
            await File.WriteAllLinesAsync(IssuesFilePath, lines, cancellationToken);
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

            var line = _serializer.SerializeIssue(issue) + Environment.NewLine;
            await File.AppendAllTextAsync(IssuesFilePath, line, cancellationToken);
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
}
