using System.Diagnostics;
using Fleece.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Fleece.Cli.Services;

public sealed class EditorService
{
    private static readonly string TemplateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".fleece",
        "templates");

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public string CreateTemplateFile()
    {
        Directory.CreateDirectory(TemplateDirectory);
        var fileName = $"{Guid.NewGuid():N}.yaml";
        var filePath = Path.Combine(TemplateDirectory, fileName);

        var template = """
            # Fleece Issue Template
            # Fill in the required fields and save the file.
            # Lines starting with # are comments and will be ignored.

            # Required fields:
            title:
            type: task  # Options: task, bug, chore, feature

            # Optional fields:
            description:
            status: open  # Options: open, progress, review, complete, archived, closed
            priority:   # 1-5 (1=highest)
            assignedTo:
            tags:   # Comma-separated list, e.g.: urgent,backend,api
            workingBranchId:   # Git branch name for this issue
            linkedPr:
            linkedIssues:   # Comma-separated issue IDs
            parentIssues:   # Comma-separated issue IDs with optional sort order (e.g., abc123,def456:bbb)
            """;

        File.WriteAllText(filePath, template);
        return filePath;
    }

    public string CreateEditTemplateFile(Issue issue)
    {
        Directory.CreateDirectory(TemplateDirectory);
        var fileName = $"{Guid.NewGuid():N}.yaml";
        var filePath = Path.Combine(TemplateDirectory, fileName);

        var linkedIssuesStr = issue.LinkedIssues.Count > 0 ? string.Join(", ", issue.LinkedIssues) : "";
        var parentIssuesStr = issue.ParentIssues.Count > 0 ? string.Join(", ", issue.ParentIssues.Select(p => p.ParentIssue)) : "";
        var tagsStr = issue.Tags.Count > 0 ? string.Join(", ", issue.Tags) : "";

        var template = $"""
            # Fleece Issue Editor - ID: {issue.Id}
            # Modify the fields below and save the file.
            # Lines starting with # are comments and will be ignored.

            # Required fields:
            title: {EscapeYamlValue(issue.Title)}
            type: {issue.Type.ToString().ToLowerInvariant()}  # Options: task, bug, chore, feature

            # Optional fields:
            description: {EscapeYamlValue(issue.Description)}
            status: {issue.Status.ToString().ToLowerInvariant()}  # Options: open, progress, review, complete, archived, closed
            priority: {(issue.Priority.HasValue ? issue.Priority.Value.ToString() : "")}  # 1-5 (1=highest)
            assignedTo: {EscapeYamlValue(issue.AssignedTo)}
            tags: {tagsStr}  # Comma-separated list, e.g.: urgent,backend,api
            workingBranchId: {EscapeYamlValue(issue.WorkingBranchId)}  # Git branch name for this issue
            linkedPr: {(issue.LinkedPR.HasValue ? issue.LinkedPR.Value.ToString() : "")}
            linkedIssues: {linkedIssuesStr}  # Comma-separated issue IDs
            parentIssues: {parentIssuesStr}  # Comma-separated issue IDs with optional sort order (e.g., abc123,def456:bbb)
            """;

        File.WriteAllText(filePath, template);
        return filePath;
    }

    private static string EscapeYamlValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        // If the value contains special characters, wrap in quotes
        if (value.Contains(':') || value.Contains('#') || value.Contains('\n') ||
            value.StartsWith(' ') || value.EndsWith(' '))
        {
            return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        }
        return value;
    }

    public void OpenEditor(string filePath)
    {
        var editor = GetEditorCommand();
        var startInfo = new ProcessStartInfo
        {
            FileName = editor,
            Arguments = $"\"{filePath}\"",
            UseShellExecute = true
        };

        using var process = Process.Start(startInfo);
        process?.WaitForExit();
    }

    public IssueTemplate? ParseTemplate(string filePath)
    {
        var content = File.ReadAllText(filePath);

        // Remove comment lines and empty lines at the start
        var lines = content.Split('\n')
            .Where(line => !line.TrimStart().StartsWith('#'))
            .ToArray();
        var cleanedContent = string.Join('\n', lines);

        if (string.IsNullOrWhiteSpace(cleanedContent))
        {
            return null;
        }

        try
        {
            return YamlDeserializer.Deserialize<IssueTemplate>(cleanedContent);
        }
        catch
        {
            return null;
        }
    }

    public void CleanupTemplateFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private static string GetEditorCommand()
    {
        // Check common environment variables for editor preference
        var editor = Environment.GetEnvironmentVariable("VISUAL")
            ?? Environment.GetEnvironmentVariable("EDITOR");

        if (!string.IsNullOrEmpty(editor))
        {
            return editor;
        }

        // Platform-specific defaults
        if (OperatingSystem.IsWindows())
        {
            return "notepad";
        }
        else if (OperatingSystem.IsMacOS())
        {
            return "open -W -t"; // -W waits, -t opens in default text editor
        }
        else
        {
            // Linux - try common editors
            var editors = new[] { "nano", "vim", "vi" };
            foreach (var e in editors)
            {
                try
                {
                    var result = Process.Start(new ProcessStartInfo
                    {
                        FileName = "which",
                        Arguments = e,
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    });
                    result?.WaitForExit();
                    if (result?.ExitCode == 0)
                    {
                        return e;
                    }
                }
                catch
                {
                    continue;
                }
            }
            return "vi";
        }
    }
}

public sealed class IssueTemplate
{
    public string? Title { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public int? Priority { get; set; }
    public string? AssignedTo { get; set; }
    public string? Tags { get; set; }
    public int? LinkedPr { get; set; }
    public string? LinkedIssues { get; set; }
    public string? ParentIssues { get; set; }
    public string? WorkingBranchId { get; set; }
}
