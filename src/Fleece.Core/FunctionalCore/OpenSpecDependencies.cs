using System.Text;
using System.Text.RegularExpressions;
using Fleece.Core.Models;

namespace Fleece.Core.FunctionalCore;

/// <summary>
/// Pure helpers for the <c>openspec dependencies</c> visualizer: parses the
/// <c>depends-on</c> YAML frontmatter of <c>openspec/changes/&lt;name&gt;/dependencies.md</c>
/// files and projects each change into a graph node by reusing the issue model so the
/// existing <c>next</c> graph-layout renderer can draw the DAG.
/// </summary>
public static partial class OpenSpecDependencies
{
    /// <summary>
    /// Parses the <c>depends-on</c> list from a <c>dependencies.md</c> file body. Only the
    /// leading YAML frontmatter block (delimited by <c>---</c>) is considered; soft dependencies
    /// written in the HTML-comment body are ignored.
    /// </summary>
    public static IReadOnlyList<string> ParseDependsOn(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var frontmatter = ExtractFrontmatter(content);
        if (frontmatter is null)
        {
            return [];
        }

        // Strip HTML comments so any change names hidden in a comment produce no edges.
        frontmatter = HtmlCommentRegex().Replace(frontmatter, string.Empty);
        return ParseDependsOnField(frontmatter);
    }

    /// <summary>
    /// Projects a map of change-name → its <c>depends-on</c> list into graph nodes. Each change
    /// becomes an <see cref="Issue"/> whose parent references are its dependencies, so the issue
    /// layout engine renders an edge from each dependant to each change it depends on.
    /// </summary>
    public static IReadOnlyList<Issue> BuildGraphNodes(
        IReadOnlyDictionary<string, IReadOnlyList<string>> dependsOn)
    {
        var result = new List<Issue>(dependsOn.Count);

        foreach (var (change, deps) in dependsOn.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            var parents = ParentIssueRef.ParseFromStrings(
                string.Join(",", deps.Where(d => !string.IsNullOrWhiteSpace(d))));

            result.Add(new Issue
            {
                Id = change,
                Title = string.Empty,
                Status = IssueStatus.Open,
                Type = IssueType.Task,
                ParentIssues = parents,
                CreatedAt = DateTimeOffset.UnixEpoch,
                LastUpdate = DateTimeOffset.UnixEpoch
            });
        }

        return result;
    }

    private static string? ExtractFrontmatter(string content)
    {
        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.Split('\n');

        var start = 0;
        while (start < lines.Length && lines[start].Trim().Length == 0)
        {
            start++;
        }

        if (start >= lines.Length || lines[start].Trim() != "---")
        {
            return null;
        }

        var sb = new StringBuilder();
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                return sb.ToString();
            }
            sb.Append(lines[i]).Append('\n');
        }

        // No closing delimiter — treat as no valid frontmatter.
        return null;
    }

    private static IReadOnlyList<string> ParseDependsOnField(string frontmatter)
    {
        var lines = frontmatter.Split('\n');
        var result = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith("depends-on:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var afterColon = trimmed["depends-on:".Length..].Trim();

            if (afterColon.StartsWith('['))
            {
                // Inline flow list: [a, b, c]
                var inner = afterColon.Trim('[', ']', ' ');
                foreach (var item in inner.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    AddItem(result, item);
                }
            }
            else if (afterColon.Length > 0)
            {
                // Single scalar on the same line.
                AddItem(result, afterColon);
            }
            else
            {
                // Block list on the following indented lines.
                for (var j = i + 1; j < lines.Length; j++)
                {
                    var itemTrimmed = lines[j].TrimStart();
                    if (itemTrimmed.Length == 0)
                    {
                        continue;
                    }
                    if (itemTrimmed.StartsWith('-'))
                    {
                        AddItem(result, itemTrimmed[1..]);
                    }
                    else
                    {
                        // A new (non-list) key ends the block.
                        break;
                    }
                }
            }

            break;
        }

        return result;
    }

    private static void AddItem(List<string> target, string raw)
    {
        var name = raw.Trim().Trim('"', '\'').Trim();
        if (name.Length > 0)
        {
            target.Add(name);
        }
    }

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlCommentRegex();
}
