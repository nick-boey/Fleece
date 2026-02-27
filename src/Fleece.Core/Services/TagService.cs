using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class TagService : ITagService
{
    /// <summary>
    /// Reserved key names that cannot be used as tag keys.
    /// These match Issue property names to avoid confusion with property-based filtering.
    /// </summary>
    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id",
        "title",
        "description",
        "status",
        "type",
        "priority",
        "linkedpr",
        "linkedissues",
        "parentissues",
        "assignedto",
        "tags",
        "questions",
        "workingbranchid",
        "executionmode",
        "createdby",
        "createdat",
        "lastupdate"
    };

    public string? ValidateTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return "Tag cannot be empty or whitespace";
        }

        // Check for whitespace anywhere in the tag
        if (tag.Any(char.IsWhiteSpace))
        {
            return $"Tag '{tag}' contains whitespace which is not allowed";
        }

        // Count equals signs
        var equalsCount = tag.Count(c => c == '=');

        if (equalsCount == 0)
        {
            // Simple tag - no further validation needed
            return null;
        }

        if (equalsCount > 1)
        {
            return $"Tag '{tag}' contains multiple '=' characters (only one allowed for key=value format)";
        }

        // Parse key=value
        var equalsIndex = tag.IndexOf('=');
        var key = tag[..equalsIndex];
        var value = tag[(equalsIndex + 1)..];

        if (string.IsNullOrEmpty(key))
        {
            return $"Tag '{tag}' has empty key (format should be key=value)";
        }

        if (string.IsNullOrEmpty(value))
        {
            return $"Tag '{tag}' has empty value (format should be key=value)";
        }

        // Check for reserved keys
        if (ReservedKeys.Contains(key))
        {
            return $"Tag key '{key}' is reserved (conflicts with issue property name)";
        }

        return null;
    }

    public IReadOnlyList<string> ValidateTags(IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        var errors = new List<string>();

        foreach (var tag in tags)
        {
            var error = ValidateTag(tag);
            if (error is not null)
            {
                errors.Add(error);
            }
        }

        return errors;
    }

    public (string Key, string? Value) ParseTag(string tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return (string.Empty, null);
        }

        var equalsIndex = tag.IndexOf('=');

        if (equalsIndex < 0)
        {
            // Simple tag
            return (tag, null);
        }

        var key = tag[..equalsIndex];
        var value = tag[(equalsIndex + 1)..];

        return (key, value);
    }

    public bool HasKeyedTag(Issue issue, string key, string value)
    {
        if (issue.Tags is null || issue.Tags.Count == 0)
        {
            return false;
        }

        foreach (var tag in issue.Tags)
        {
            var (tagKey, tagValue) = ParseTag(tag);

            if (tagValue is not null &&
                tagKey.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                tagValue.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetKeyedTags(Issue issue)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (issue.Tags is null || issue.Tags.Count == 0)
        {
            return result.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<string>)kvp.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        foreach (var tag in issue.Tags)
        {
            var (key, value) = ParseTag(tag);

            if (value is not null)
            {
                if (!result.TryGetValue(key, out var values))
                {
                    values = [];
                    result[key] = values;
                }
                values.Add(value);
            }
        }

        return result.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value,
            StringComparer.OrdinalIgnoreCase);
    }
}
