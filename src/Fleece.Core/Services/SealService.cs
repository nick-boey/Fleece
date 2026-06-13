using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Default <see cref="ISealService"/> implementation over the per-issue append-only log store.
/// </summary>
/// <remarks>
/// Seal is the "finish the branch" operation: it refuses unless every issue is inactive
/// (<c>Complete</c>/<c>Closed</c>/<c>Promoted</c>), otherwise it writes a content-addressed
/// archive under <c>.fleece/archive/</c> and clears the live <c>.fleece/issues/</c> directory.
/// </remarks>
public sealed class SealService : ISealService
{
    private const string FleeceDirectory = ".fleece";
    private const string ArchiveDirectory = "archive";
    private const string LogFileExtension = ".jsonl";

    /// <summary>Length (hex chars) of the content hash embedded in the archive file name.</summary>
    private const int HashLength = 12;

    private readonly IFleeceService _fleeceService;
    private readonly IEventStore _eventStore;
    private readonly IFileSystem _fileSystem;
    private readonly string _basePath;

    public SealService(
        IFleeceService fleeceService,
        IEventStore eventStore,
        string basePath,
        IFileSystem fileSystem)
    {
        _fleeceService = fleeceService;
        _eventStore = eventStore;
        _basePath = basePath;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public async Task<SealResult> SealAsync(CancellationToken cancellationToken = default)
    {
        var issues = await _fleeceService.GetAllAsync(cancellationToken);

        // Empty issue set is a no-op success: nothing to archive or clear.
        if (issues.Count == 0)
        {
            return new SealResult
            {
                Sealed = true,
                ActiveIssues = [],
                ArchivePath = null,
                RemovedCount = 0,
            };
        }

        // Refuse while any issue is still active ({Open, Progress, Review}); make no changes.
        var activeIssues = issues
            .Where(i => i.Status.IsActive())
            .OrderBy(i => i.Id, StringComparer.Ordinal)
            .ToList();

        if (activeIssues.Count > 0)
        {
            return new SealResult
            {
                Sealed = false,
                ActiveIssues = activeIssues,
                ArchivePath = null,
                RemovedCount = 0,
            };
        }

        // Canonicalise: sort by id and serialize deterministically so identical logical
        // state yields one stable archive name, independent of file/replay ordering.
        var content = SerializeCanonical(issues);
        var hash = ComputeContentHash(content);

        var archiveDirectory = _fileSystem.Path.Combine(_basePath, FleeceDirectory, ArchiveDirectory);
        if (!_fileSystem.Directory.Exists(archiveDirectory))
        {
            _fileSystem.Directory.CreateDirectory(archiveDirectory);
        }

        var archivePath = _fileSystem.Path.Combine(archiveDirectory, $"issues_{hash}{LogFileExtension}");
        await _fileSystem.File.WriteAllTextAsync(archivePath, content, cancellationToken);

        // Clear every live per-issue log so the branch becomes mergeable (CI-gate invariant).
        var logPaths = await _eventStore.GetAllIssueLogPathsAsync(cancellationToken);
        foreach (var logPath in logPaths)
        {
            var issueId = _fileSystem.Path.GetFileNameWithoutExtension(logPath);
            await _eventStore.DeleteIssueLogAsync(issueId, cancellationToken);
        }

        return new SealResult
        {
            Sealed = true,
            ActiveIssues = [],
            ArchivePath = archivePath,
            RemovedCount = logPaths.Count,
        };
    }

    private static string SerializeCanonical(IReadOnlyList<Issue> issues)
    {
        var builder = new StringBuilder();
        foreach (var issue in issues.OrderBy(i => i.Id, StringComparer.Ordinal))
        {
            builder.Append(JsonSerializer.Serialize(issue, FleeceJsonContext.Default.Issue));
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return hashString[..HashLength];
    }
}
