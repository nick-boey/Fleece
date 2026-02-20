namespace Fleece.Core.Models;

/// <summary>
/// Represents the final effective settings after merging global, local, and CLI overrides.
/// All properties have concrete values after applying defaults.
/// </summary>
public sealed record EffectiveSettings
{
    /// <summary>
    /// Whether to automatically merge issues before operations.
    /// </summary>
    public required bool AutoMerge { get; init; }

    /// <summary>
    /// User identity for ModifiedBy fields, or null to use git config.
    /// </summary>
    public required string? Identity { get; init; }

    /// <summary>
    /// Branch for issue synchronization, or null to use current branch.
    /// </summary>
    public required string? SyncBranch { get; init; }

    /// <summary>
    /// Source of each setting for debugging/display purposes.
    /// </summary>
    public required SettingsSources Sources { get; init; }
}

/// <summary>
/// Tracks the source of each setting value for display in config --list.
/// </summary>
public sealed record SettingsSources
{
    public required SettingSource AutoMerge { get; init; }
    public required SettingSource Identity { get; init; }
    public required SettingSource SyncBranch { get; init; }
}

/// <summary>
/// Indicates where a setting value originated from.
/// </summary>
public enum SettingSource
{
    /// <summary>Built-in default value.</summary>
    Default,

    /// <summary>From ~/.fleece/settings.json.</summary>
    Global,

    /// <summary>From .fleece/settings.json in the repository.</summary>
    Local,

    /// <summary>From command-line argument.</summary>
    CommandLine
}
