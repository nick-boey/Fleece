using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Service for loading, saving, and merging Fleece configuration settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Load settings from a specific file path.
    /// Returns null if the file does not exist.
    /// </summary>
    Task<FleeceSettings?> LoadSettingsFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load global settings from ~/.fleece/settings.json.
    /// </summary>
    Task<FleeceSettings?> LoadGlobalSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Load local settings from .fleece/settings.json in the current project.
    /// </summary>
    Task<FleeceSettings?> LoadLocalSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save settings to a specific file path.
    /// </summary>
    Task SaveSettingsToFileAsync(string filePath, FleeceSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save global settings to ~/.fleece/settings.json.
    /// </summary>
    Task SaveGlobalSettingsAsync(FleeceSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save local settings to .fleece/settings.json.
    /// </summary>
    Task SaveLocalSettingsAsync(FleeceSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get effective settings by merging global, local, and CLI overrides.
    /// Priority: CLI > local > global > defaults.
    /// </summary>
    Task<EffectiveSettings> GetEffectiveSettingsAsync(
        FleeceSettings? cliOverrides = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a single setting value in a settings file.
    /// Creates the file if it doesn't exist.
    /// </summary>
    /// <param name="key">The setting key (e.g., "autoMerge", "identity", "syncBranch").</param>
    /// <param name="value">The value to set. Empty string clears the setting.</param>
    /// <param name="global">If true, modifies global settings; otherwise modifies local settings.</param>
    Task SetSettingAsync(
        string key,
        string value,
        bool global = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the path to the global settings file.
    /// </summary>
    string GetGlobalSettingsPath();

    /// <summary>
    /// Get the path to the local settings file.
    /// </summary>
    string GetLocalSettingsPath();
}
