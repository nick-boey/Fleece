using System.Text.Json;
using Fleece.Core.Models;

namespace Fleece.Core.Serialization;

public interface IJsonlSerializer
{
    string SerializeIssue(Issue issue);
    Issue? DeserializeIssue(string line);
    string SerializeConflict(ConflictRecord conflict);
    ConflictRecord? DeserializeConflict(string line);
    IReadOnlyList<Issue> DeserializeIssues(string content);
    IReadOnlyList<ConflictRecord> DeserializeConflicts(string content);
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

    public string SerializeConflict(ConflictRecord conflict)
    {
        return JsonSerializer.Serialize(conflict, FleeceJsonContext.Default.ConflictRecord);
    }

    public ConflictRecord? DeserializeConflict(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(line, FleeceJsonContext.Default.ConflictRecord);
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

    public IReadOnlyList<ConflictRecord> DeserializeConflicts(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var conflicts = new List<ConflictRecord>();
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var conflict = DeserializeConflict(line.Trim());
            if (conflict is not null)
            {
                conflicts.Add(conflict);
            }
        }

        return conflicts;
    }
}
