namespace Fleece.Core.Models;

/// <summary>
/// Represents Fleece configuration settings that can be specified globally (~/.fleece/settings.json)
/// or locally (.fleece/settings.json). All properties are nullable to support partial settings files
/// where only specific settings are overridden.
/// </summary>
public sealed record FleeceSettings
{
    /// <summary>
    /// When true, automatically run merge before operations that read issues.
    /// Default: false
    /// </summary>
    public bool? AutoMerge { get; init; }

    /// <summary>
    /// User identity for ModifiedBy fields. Overrides git config user.name when set.
    /// Default: null (falls back to git config)
    /// </summary>
    public string? Identity { get; init; }

    /// <summary>
    /// Separate branch for issue synchronization.
    /// Default: null (uses current branch)
    /// </summary>
    public string? SyncBranch { get; init; }
}
