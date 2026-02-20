using System.Text.Json;
using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Service for loading, saving, and merging Fleece configuration settings.
/// Supports global settings (~/.fleece/settings.json) and local settings (.fleece/settings.json).
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly string GlobalSettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".fleece");

    private static readonly string GlobalSettingsFilePath = Path.Combine(
        GlobalSettingsDirectory,
        "settings.json");

    private readonly string _localSettingsPath;

    public SettingsService(string basePath)
    {
        _localSettingsPath = Path.Combine(basePath, ".fleece", "settings.json");
    }

    public string GetGlobalSettingsPath() => GlobalSettingsFilePath;

    public string GetLocalSettingsPath() => _localSettingsPath;

    public async Task<FleeceSettings?> LoadSettingsFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize(json, FleeceJsonContext.Default.FleeceSettings);
        }
        catch (JsonException)
        {
            // Return null for malformed JSON rather than throwing
            return null;
        }
    }

    public Task<FleeceSettings?> LoadGlobalSettingsAsync(CancellationToken cancellationToken = default)
        => LoadSettingsFromFileAsync(GlobalSettingsFilePath, cancellationToken);

    public Task<FleeceSettings?> LoadLocalSettingsAsync(CancellationToken cancellationToken = default)
        => LoadSettingsFromFileAsync(_localSettingsPath, cancellationToken);

    public async Task SaveSettingsToFileAsync(string filePath, FleeceSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Use FleeceSettingsWriteContext which has WriteIndented = true for human-readable output
        var json = JsonSerializer.Serialize(settings, FleeceSettingsWriteContext.Default.FleeceSettings);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    public Task SaveGlobalSettingsAsync(FleeceSettings settings, CancellationToken cancellationToken = default)
        => SaveSettingsToFileAsync(GlobalSettingsFilePath, settings, cancellationToken);

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
        var filePath = global ? GlobalSettingsFilePath : _localSettingsPath;
        var existing = await LoadSettingsFromFileAsync(filePath, cancellationToken) ?? new FleeceSettings();

        var updated = key.ToLowerInvariant() switch
        {
            "automerge" => existing with { AutoMerge = ParseBool(value) },
            "identity" => existing with { Identity = string.IsNullOrEmpty(value) ? null : value },
            "syncbranch" => existing with { SyncBranch = string.IsNullOrEmpty(value) ? null : value },
            _ => throw new ArgumentException($"Unknown setting: {key}. Valid settings are: autoMerge, identity, syncBranch")
        };

        await SaveSettingsToFileAsync(filePath, updated, cancellationToken);
    }

    private static bool? ParseBool(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => throw new ArgumentException($"Invalid boolean value: {value}. Use true/false, yes/no, or 1/0.")
        };
    }

    private static EffectiveSettings MergeSettings(
        FleeceSettings? global,
        FleeceSettings? local,
        FleeceSettings? cli)
    {
        var (autoMerge, autoMergeSource) = ResolveBoolValue(
            cli?.AutoMerge, local?.AutoMerge, global?.AutoMerge, false);
        var (identity, identitySource) = ResolveValue(
            cli?.Identity, local?.Identity, global?.Identity, (string?)null);
        var (syncBranch, syncBranchSource) = ResolveValue(
            cli?.SyncBranch, local?.SyncBranch, global?.SyncBranch, (string?)null);

        return new EffectiveSettings
        {
            AutoMerge = autoMerge,
            Identity = identity,
            SyncBranch = syncBranch,
            Sources = new SettingsSources
            {
                AutoMerge = autoMergeSource,
                Identity = identitySource,
                SyncBranch = syncBranchSource
            }
        };
    }

    private static (bool value, SettingSource source) ResolveBoolValue(
        bool? cliValue, bool? localValue, bool? globalValue, bool defaultValue)
    {
        if (cliValue.HasValue)
        {
            return (cliValue.Value, SettingSource.CommandLine);
        }

        if (localValue.HasValue)
        {
            return (localValue.Value, SettingSource.Local);
        }

        if (globalValue.HasValue)
        {
            return (globalValue.Value, SettingSource.Global);
        }

        return (defaultValue, SettingSource.Default);
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
