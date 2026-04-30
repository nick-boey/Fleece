using System.IO.Abstractions;
using System.Text.Json;
using Fleece.Core.EventSourcing.Services.Interfaces;

namespace Fleece.Core.EventSourcing.Services;

public sealed class ReplayCache : IReplayCache
{
    internal const string FleeceDirectory = ".fleece";
    internal const string CacheFileName = ".replay-cache";

    private readonly string _basePath;
    private readonly IFileSystem _fileSystem;

    public ReplayCache(string basePath, IFileSystem? fileSystem = null)
    {
        _basePath = basePath;
        _fileSystem = fileSystem ?? new Testably.Abstractions.RealFileSystem();
    }

    private string CachePath => _fileSystem.Path.Combine(_basePath, FleeceDirectory, CacheFileName);

    public async Task<ReplayCacheFile?> TryReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.File.Exists(CachePath))
        {
            return null;
        }
        try
        {
            var content = await _fileSystem.File.ReadAllTextAsync(CachePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }
            var cache = JsonSerializer.Deserialize(content, EventSourcingJsonContext.Default.ReplayCacheFile);
            if (cache is null || string.IsNullOrEmpty(cache.HeadSha))
            {
                return null;
            }
            return cache;
        }
        catch (JsonException)
        {
            // Corrupt cache — treat as absent.
            return null;
        }
    }

    public async Task WriteAsync(string headSha, IReadOnlyDictionary<string, Issue> committedState, CancellationToken cancellationToken = default)
    {
        var dir = _fileSystem.Path.Combine(_basePath, FleeceDirectory);
        if (!_fileSystem.Directory.Exists(dir))
        {
            _fileSystem.Directory.CreateDirectory(dir);
        }
        var cache = new ReplayCacheFile
        {
            HeadSha = headSha,
            Issues = [.. committedState.Values],
        };
        var json = JsonSerializer.Serialize(cache, EventSourcingJsonContext.Default.ReplayCacheFile);
        await _fileSystem.File.WriteAllTextAsync(CachePath, json + "\n", cancellationToken);
    }

    public Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        if (_fileSystem.File.Exists(CachePath))
        {
            _fileSystem.File.Delete(CachePath);
        }
        return Task.CompletedTask;
    }
}
