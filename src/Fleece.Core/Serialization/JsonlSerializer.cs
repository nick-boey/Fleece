using System.Text.Json;
using Fleece.Core.Models;

namespace Fleece.Core.Serialization;

public interface IJsonlSerializer
{
    string SerializeIssue(Issue issue);
    Issue? DeserializeIssue(string line);
    string SerializeTombstone(Tombstone tombstone);
    Tombstone? DeserializeTombstone(string line);
    IReadOnlyList<Issue> DeserializeIssues(string content);
    IReadOnlyList<Tombstone> DeserializeTombstones(string content);
}

public sealed class JsonlSerializer : IJsonlSerializer
{
    public string SerializeIssue(Issue issue)
    {
        return JsonSerializer.Serialize(issue, FleeceJsonContext.Default.Issue);
    }

    public Issue? DeserializeIssue(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(line, FleeceJsonContext.Default.Issue);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string SerializeTombstone(Tombstone tombstone)
    {
        return JsonSerializer.Serialize(tombstone, FleeceJsonContext.Default.Tombstone);
    }

    public Tombstone? DeserializeTombstone(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(line, FleeceJsonContext.Default.Tombstone);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<Issue> DeserializeIssues(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var issues = new List<Issue>();
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var issue = DeserializeIssue(line.Trim());
            if (issue is not null)
            {
                issues.Add(issue);
            }
        }

        return issues;
    }

    public IReadOnlyList<Tombstone> DeserializeTombstones(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var tombstones = new List<Tombstone>();
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var tombstone = DeserializeTombstone(line.Trim());
            if (tombstone is not null)
            {
                tombstones.Add(tombstone);
            }
        }

        return tombstones;
    }
}
