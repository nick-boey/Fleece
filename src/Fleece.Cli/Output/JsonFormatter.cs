using System.Text.Json;
using Fleece.Core.Models;
using Fleece.Core.Serialization;

namespace Fleece.Cli.Output;

public static class JsonFormatter
{
    public static void RenderIssues(
        IReadOnlyList<Issue> issues,
        bool verbose = false,
        IReadOnlyDictionary<string, SyncStatus>? syncStatuses = null)
    {
        if (syncStatuses != null)
        {
            // Use IssueSyncDto to include sync status
            var dtos = issues.Select(issue =>
            {
                var status = syncStatuses.GetValueOrDefault(issue.Id, SyncStatus.Local);
                return IssueSyncDto.FromIssue(issue, status);
            }).ToList();
            var json = JsonSerializer.Serialize(dtos, FleeceJsonContext.Default.IReadOnlyListIssueSyncDto);
            Console.WriteLine(json);
        }
        else if (verbose)
        {
            var json = JsonSerializer.Serialize(issues, FleeceJsonContext.Default.IReadOnlyListIssue);
            Console.WriteLine(json);
        }
        else
        {
            var dtos = issues.Select(IssueDto.FromIssue).ToList();
            var json = JsonSerializer.Serialize(dtos, FleeceJsonContext.Default.IReadOnlyListIssueDto);
            Console.WriteLine(json);
        }
    }

    public static void RenderIssue(Issue issue, bool verbose = false)
    {
        if (verbose)
        {
            var json = JsonSerializer.Serialize(issue, FleeceJsonContext.Default.Issue);
            Console.WriteLine(json);
        }
        else
        {
            var dto = IssueDto.FromIssue(issue);
            var json = JsonSerializer.Serialize(dto, FleeceJsonContext.Default.IssueDto);
            Console.WriteLine(json);
        }
    }

    public static void RenderIssueShow(IssueShowDto showContext)
    {
        var json = JsonSerializer.Serialize(showContext, FleeceJsonContext.Default.IssueShowDto);
        Console.WriteLine(json);
    }
}
