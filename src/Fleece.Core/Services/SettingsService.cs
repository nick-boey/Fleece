using System.Text.Json;
using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;
using System.IO.Abstractions;

namespace Fleece.Core.Services;

/// <summary>
/// Service for loading, saving, and merging Fleece configuration settings.
/// Supports global settings (~/.fleece/settings.json) and local settings (.fleece/settings.json).
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _globalSettingsDirectory;
    private readonly string _globalSettingsFilePath;
    private readonly string _localSettingsPath;

    public SettingsService(string basePath, IFileSystem? fileSystem = null, string? globalSettingsDirectory = null)
    {
        _fileSystem = fileSystem ?? new Testably.Abstractions.RealFileSystem();
        _globalSettingsDirectory = globalSettingsDirectory ?? _fileSystem.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".fleece");
        _globalSettingsFilePath = _fileSystem.Path.Combine(_globalSettingsDirectory, "settings.json");
        _localSettingsPath = _fileSystem.Path.Combine(basePath, ".fleece", "settings.json");
    }

    public string GetGlobalSettingsPath() => _globalSettingsFilePath;

    public string GetLocalSettingsPath() => _localSettingsPath;

    public async Task<FleeceSettings?> LoadSettingsFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize(json, FleeceJsonContext.Default.FleeceSettings);
        }
        catch (JsonException)
        {
            // Return null for malformed JSON rather than throwing
            return null;
        }
    }

    public Task<FleeceSettings?> LoadGlobalSettingsAsync(CancellationToken cancellationToken = default)
        => LoadSettingsFromFileAsync(_globalSettingsFilePath, cancellationToken);

    public Task<FleeceSettings?> LoadLocalSettingsAsync(CancellationToken cancellationToken = default)
        => LoadSettingsFromFileAsync(_localSettingsPath, cancellationToken);

    public async Task SaveSettingsToFileAsync(string filePath, FleeceSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = _fileSystem.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !_fileSystem.Directory.Exists(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }

        // Use FleeceSettingsWriteContext which has WriteIndented = true for human-readable output
        var json = JsonSerializer.Serialize(settings, FleeceSettingsWriteContext.Default.FleeceSettings);
        await _fileSystem.File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    public Task SaveGlobalSettingsAsync(FleeceSettings settings, CancellationToken cancellationToken = default)
        => SaveSettingsToFileAsync(_globalSettingsFilePath, settings, cancellationToken);

    public Task SaveLocalSettingsAsync(FleeceSettings settings, CancellationToken cancellationToken = default)
        => SaveSettingsToFileAsync(_localSettingsPath, settings, cancellationToken);

    public async Task<EffectiveSettings> GetEffectiveSettingsAsync(
        FleeceSettings? cliOverrides = null,
        CancellationToken cancellationToken = default)
    {
        var global = await LoadGlobalSettingsAsync(cancellationToken);
        var local = await LoadLocalSettingsAsync(cancellationToken);

        return MergeSettings(global, local, cliOverrides);
    }

    public async Task SetSettingAsync(
        string key,
        string value,
        bool global = false,
        CancellationToken cancellationToken = default)
    {
        var filePath = global ? _globalSettingsFilePath : _localSettingsPath;
        var existing = await LoadSettingsFromFileAsync(filePath, cancellationToken) ?? new FleeceSettings();

        var updated = key.ToLowerInvariant() switch
        {
            "identity" => existing with { Identity = string.IsNullOrEmpty(value) ? null : value },
            "syncbranch" => existing with { SyncBranch = string.IsNullOrEmpty(value) ? null : value },
            "tracker" => existing with { Tracker = Trackers.Normalize(value) },
            _ => throw new ArgumentException($"Unknown setting: {key}. Valid settings are: identity, syncBranch, tracker")
        };

        await SaveSettingsToFileAsync(filePath, updated, cancellationToken);
    }

    private static EffectiveSettings MergeSettings(
        FleeceSettings? global,
        FleeceSettings? local,
        FleeceSettings? cli)
    {
        var (identity, identitySource) = ResolveValue(
            cli?.Identity, local?.Identity, global?.Identity, (string?)null);
        var (syncBranch, syncBranchSource) = ResolveValue(
            cli?.SyncBranch, local?.SyncBranch, global?.SyncBranch, (string?)null);
        var (tracker, trackerSource) = ResolveValue(
            cli?.Tracker, local?.Tracker, global?.Tracker, Trackers.Default);

        return new EffectiveSettings
        {
            Identity = identity,
            SyncBranch = syncBranch,
            Tracker = tracker,
            Sources = new SettingsSources
            {
                Identity = identitySource,
                SyncBranch = syncBranchSource,
                Tracker = trackerSource
            }
        };
    }

    private static (T value, SettingSource source) ResolveValue<T>(
        T? cliValue, T? localValue, T? globalValue, T defaultValue)
    {
        if (cliValue is not null)
        {
            return (cliValue, SettingSource.CommandLine);
        }

        if (localValue is not null)
        {
            return (localValue, SettingSource.Local);
        }

        if (globalValue is not null)
        {
            return (globalValue, SettingSource.Global);
        }

        return (defaultValue!, SettingSource.Default);
    }
}
