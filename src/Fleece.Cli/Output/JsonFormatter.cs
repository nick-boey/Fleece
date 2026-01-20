using System.Text.Json;
using Fleece.Core.Models;
using Fleece.Core.Serialization;

namespace Fleece.Cli.Output;

public static class JsonFormatter
{
    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void RenderIssues(IReadOnlyList<Issue> issues)
    {
        var json = JsonSerializer.Serialize(issues, FleeceJsonContext.Default.IReadOnlyListIssue);
        Console.WriteLine(json);
    }

    public static void RenderIssue(Issue issue)
    {
        var json = JsonSerializer.Serialize(issue, FleeceJsonContext.Default.Issue);
        Console.WriteLine(json);
    }

    public static void RenderChanges(IReadOnlyList<ChangeRecord> changes)
    {
        var json = JsonSerializer.Serialize(changes, FleeceJsonContext.Default.IReadOnlyListChangeRecord);
        Console.WriteLine(json);
    }
}
