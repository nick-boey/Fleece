namespace Fleece.Core.Models;

/// <summary>
/// Helper class for working with keyed tags in the format "key=value".
/// Keyed tags allow storing structured metadata within the tags collection.
/// </summary>
public static class KeyedTag
{
    /// <summary>
    /// The key used for linked PR tags.
    /// </summary>
    public const string LinkedPrKey = "hsp-linked-pr";

    /// <summary>
    /// The separator between key and value in a keyed tag.
    /// </summary>
    private const char Separator = '=';

    /// <summary>
    /// Attempts to parse a tag string into a key-value pair.
    /// </summary>
    /// <param name="tag">The tag string to parse.</param>
    /// <param name="key">The extracted key, or null if parsing fails.</param>
    /// <param name="value">The extracted value, or null if parsing fails.</param>
    /// <returns>True if the tag was successfully parsed as a key=value pair.</returns>
    public static bool TryParse(string tag, out string? key, out string? value)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            key = null;
            value = null;
            return false;
        }

        var separatorIndex = tag.IndexOf(Separator);
        if (separatorIndex <= 0 || separatorIndex == tag.Length - 1)
        {
            key = null;
            value = null;
            return false;
        }

        key = tag[..separatorIndex];
        value = tag[(separatorIndex + 1)..];
        return true;
    }

    /// <summary>
    /// Creates a keyed tag string from a key and value.
    /// </summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>A string in the format "key=value".</returns>
    public static string Create(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return $"{key}{Separator}{value}";
    }

    /// <summary>
    /// Gets all values for a specific key from a collection of tags.
    /// </summary>
    /// <param name="tags">The collection of tags to search.</param>
    /// <param name="key">The key to search for.</param>
    /// <returns>An enumerable of values associated with the key.</returns>
    public static IEnumerable<string> GetValues(IEnumerable<string>? tags, string key)
    {
        if (tags is null)
        {
            yield break;
        }

        foreach (var tag in tags)
        {
            if (TryParse(tag, out var tagKey, out var tagValue) &&
                string.Equals(tagKey, key, StringComparison.OrdinalIgnoreCase) &&
                tagValue is not null)
            {
                yield return tagValue;
            }
        }
    }

    /// <summary>
    /// Replaces all values for a specific key with a new set of values.
    /// </summary>
    /// <param name="tags">The existing tags collection.</param>
    /// <param name="key">The key to set values for.</param>
    /// <param name="values">The new values to set.</param>
    /// <returns>A new tags collection with the updated values.</returns>
    public static IReadOnlyList<string> SetValues(IReadOnlyList<string>? tags, string key, IEnumerable<string> values)
    {
        var result = new List<string>();

        // Keep all tags that don't match the key
        if (tags is not null)
        {
            foreach (var tag in tags)
            {
                if (!TryParse(tag, out var tagKey, out _) ||
                    !string.Equals(tagKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(tag);
                }
            }
        }

        // Add new values
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(Create(key, value));
            }
        }

        return result;
    }

    /// <summary>
    /// Adds a value for a specific key if it doesn't already exist.
    /// </summary>
    /// <param name="tags">The existing tags collection.</param>
    /// <param name="key">The key to add a value for.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>A new tags collection with the added value.</returns>
    public static IReadOnlyList<string> AddValue(IReadOnlyList<string>? tags, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var newTag = Create(key, value);
        var result = tags?.ToList() ?? [];

        // Check if this exact key=value already exists (case-insensitive)
        var exists = result.Any(t =>
            TryParse(t, out var tagKey, out var tagValue) &&
            string.Equals(tagKey, key, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(tagValue, value, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            result.Add(newTag);
        }

        return result;
    }

    /// <summary>
    /// Removes a specific value for a key from the tags collection.
    /// </summary>
    /// <param name="tags">The existing tags collection.</param>
    /// <param name="key">The key to remove a value from.</param>
    /// <param name="value">The value to remove.</param>
    /// <returns>A new tags collection with the value removed.</returns>
    public static IReadOnlyList<string> RemoveValue(IReadOnlyList<string>? tags, string key, string value)
    {
        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        return tags
            .Where(t =>
            {
                if (!TryParse(t, out var tagKey, out var tagValue))
                {
                    return true; // Keep non-keyed tags
                }

                // Remove only if both key and value match
                return !string.Equals(tagKey, key, StringComparison.OrdinalIgnoreCase) ||
                       !string.Equals(tagValue, value, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }

    /// <summary>
    /// Checks if a tag collection contains a specific key.
    /// </summary>
    /// <param name="tags">The tags collection to check.</param>
    /// <param name="key">The key to search for.</param>
    /// <returns>True if any tag has the specified key.</returns>
    public static bool HasKey(IEnumerable<string>? tags, string key)
    {
        return GetValues(tags, key).Any();
    }
}
