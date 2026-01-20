using System.Text.Json;
using Fleece.Core.Models;

namespace Fleece.Core.Serialization;

public interface IJsonlSerializer
{
    string SerializeIssue(Issue issue);
    Issue? DeserializeIssue(string line);
    string SerializeChange(ChangeRecord change);
    ChangeRecord? DeserializeChange(string line);
    IReadOnlyList<Issue> DeserializeIssues(string content);
    IReadOnlyList<ChangeRecord> DeserializeChanges(string content);
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

    public string SerializeChange(ChangeRecord change)
    {
        return JsonSerializer.Serialize(change, FleeceJsonContext.Default.ChangeRecord);
    }

    public ChangeRecord? DeserializeChange(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(line, FleeceJsonContext.Default.ChangeRecord);
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

    public IReadOnlyList<ChangeRecord> DeserializeChanges(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var changes = new List<ChangeRecord>();
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var change = DeserializeChange(line.Trim());
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        return changes;
    }
}
