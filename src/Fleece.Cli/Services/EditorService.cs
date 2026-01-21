using System.Diagnostics;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Fleece.Cli.Services;

public sealed class EditorService
{
    private static readonly string CreateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".fleece",
        "create");

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public string CreateTemplateFile()
    {
        Directory.CreateDirectory(CreateDirectory);
        var fileName = $"{Guid.NewGuid():N}.yaml";
        var filePath = Path.Combine(CreateDirectory, fileName);

        var template = """
            # Fleece Issue Template
            # Fill in the required fields and save the file.
            # Lines starting with # are comments and will be ignored.

            # Required fields:
            title:
            type: task  # Options: task, bug, chore, idea, feature

            # Optional fields:
            description:
            status: open  # Options: open, complete, closed, archived
            priority:   # 1-5 (1=highest)
            group:
            assignedTo:
            tags:   # Comma-separated list, e.g.: urgent,backend,api
            linkedPr:
            linkedIssues:   # Comma-separated issue IDs
            parentIssues:   # Comma-separated issue IDs
            """;

        File.WriteAllText(filePath, template);
        return filePath;
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
    public string? Group { get; set; }
    public string? AssignedTo { get; set; }
    public string? Tags { get; set; }
    public int? LinkedPr { get; set; }
    public string? LinkedIssues { get; set; }
    public string? ParentIssues { get; set; }
}
