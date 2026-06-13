using System.IO.Abstractions;
using System.Text.Json;
using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

/// <summary>
/// Renders a DAG of OpenSpec changes built from the <c>depends-on</c> frontmatter of
/// <c>openspec/changes/&lt;name&gt;/dependencies.md</c> files. Pure read-only visualizer:
/// reuses the <c>next</c> graph-layout renderer for the drawing and <c>validate</c>'s cycle
/// detection to warn on circular change dependencies.
/// </summary>
public sealed class OpenSpecDependenciesCommand(
    IFileSystem fileSystem,
    IIssueLayoutService issueLayoutService,
    BasePathProvider basePathProvider,
    IAnsiConsole console) : AsyncCommand<OpenSpecDependenciesSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, OpenSpecDependenciesSettings settings)
    {
        var changesDir = fileSystem.Path.Combine(basePathProvider.BasePath, "openspec", "changes");
        if (!fileSystem.Directory.Exists(changesDir))
        {
            if (settings.Json)
            {
                RenderJson([], new DependencyValidationResult(true, []));
            }
            else
            {
                console.MarkupLine("[dim]No OpenSpec changes directory found.[/]");
            }
            return Task.FromResult(0);
        }

        var dependsOn = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in fileSystem.Directory.GetDirectories(changesDir))
        {
            var name = fileSystem.Path.GetFileName(dir);

            // The archive holds completed changes; it is not part of the active dependency graph.
            if (string.Equals(name, "archive", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            IReadOnlyList<string> deps = [];
            var depFile = fileSystem.Path.Combine(dir, "dependencies.md");
            if (fileSystem.File.Exists(depFile))
            {
                deps = OpenSpecDependencies.ParseDependsOn(fileSystem.File.ReadAllText(depFile));
            }

            dependsOn[name] = deps;
        }

        var issues = OpenSpecDependencies.BuildGraphNodes(dependsOn);
        var validation = Validation.ValidateDependencyCycles(issues);

        if (settings.Json)
        {
            RenderJson(issues, validation);
            return Task.FromResult(0);
        }

        if (issues.Count == 0)
        {
            console.MarkupLine("[dim]No OpenSpec changes found.[/]");
            return Task.FromResult(0);
        }

        if (!validation.IsValid)
        {
            console.MarkupLine($"[yellow]Warning:[/] {validation.Cycles.Count} circular change dependency(ies) detected:");
            foreach (var cycle in validation.Cycles)
            {
                console.MarkupLine($"  [yellow]{Markup.Escape(string.Join(" → ", cycle.IssueIds))}[/]");
            }
            console.WriteLine();

            // A cyclic graph has no clean DAG to lay out; the warning above is the deliverable.
            return Task.FromResult(0);
        }

        var graph = issueLayoutService.LayoutForTree(issues, visibility: InactiveVisibility.Always);
        TaskGraphRenderer.Render(console, graph);
        return Task.FromResult(0);
    }

    private void RenderJson(IReadOnlyList<Issue> issues, DependencyValidationResult validation)
    {
        var nodeIds = new HashSet<string>(issues.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
        var edges = issues
            .SelectMany(i => i.ActiveParentIssues
                .Where(p => nodeIds.Contains(p.ParentIssue))
                .Select(p => new { from = i.Id, to = p.ParentIssue }))
            .ToArray();

        var output = new
        {
            nodes = issues.Select(i => i.Id).ToArray(),
            edges,
            valid = validation.IsValid,
            cycles = validation.Cycles.Select(c => new { changes = c.IssueIds }).ToArray()
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        console.WriteLine(JsonSerializer.Serialize(output, options));
    }
}
