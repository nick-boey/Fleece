using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

public interface ITagService
{
    /// <summary>
    /// Validates a single tag string.
    /// Returns null if valid, or an error message if invalid.
    /// </summary>
    string? ValidateTag(string tag);

    /// <summary>
    /// Validates multiple tags.
    /// Returns a list of validation errors (empty if all valid).
    /// </summary>
    IReadOnlyList<string> ValidateTags(IReadOnlyList<string>? tags);

    /// <summary>
    /// Parses a tag into key and optional value.
    /// If the tag contains '=', returns (key, value).
    /// Otherwise returns (tag, null) for simple string tags.
    /// </summary>
    (string Key, string? Value) ParseTag(string tag);

    /// <summary>
    /// Checks if an issue has a keyed tag matching the given key and value.
    /// Uses case-insensitive matching for both key and value.
    /// </summary>
    bool HasKeyedTag(Issue issue, string key, string value);

    /// <summary>
    /// Gets all keyed tags from an issue as key-value pairs.
    /// Only returns tags that contain '='.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> GetKeyedTags(Issue issue);
}
