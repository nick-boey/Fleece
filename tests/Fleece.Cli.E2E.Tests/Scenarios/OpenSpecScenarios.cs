using System.Text.Json;

namespace Fleece.Cli.E2E.Tests.Scenarios;

/// <summary>
/// Exercises <c>fleece openspec dependencies</c>: builds and renders a DAG of OpenSpec changes
/// from the <c>depends-on</c> frontmatter of their <c>dependencies.md</c> files.
/// </summary>
[TestFixture]
[Category("openspec dependencies")]
public class OpenSpecScenarios : CliScenarioTestBase
{
    private void WriteChange(string name, string? dependenciesMd)
    {
        var dir = Path.Combine(BasePath, "openspec", "changes", name);
        Fs.Directory.CreateDirectory(dir);
        if (dependenciesMd is not null)
        {
            Fs.File.WriteAllText(Path.Combine(dir, "dependencies.md"), dependenciesMd);
        }
    }

    private static string Frontmatter(params string[] dependsOn)
        => $"---\ndepends-on: [{string.Join(", ", dependsOn)}]\n---\n";

    [Test]
    public async Task Dependencies_builds_edges_from_depends_on_frontmatter()
    {
        WriteChange("change-a", dependenciesMd: null);
        WriteChange("change-b", Frontmatter("change-a"));

        var exit = await RunAsync("openspec", "dependencies", "--json");
        exit.Should().Be(0);

        var root = JsonDocument.Parse(Console.Output).RootElement;
        root.GetProperty("nodes").EnumerateArray().Select(e => e.GetString())
            .Should().Contain(["change-a", "change-b"]);

        var edges = root.GetProperty("edges").EnumerateArray()
            .Select(e => (From: e.GetProperty("from").GetString(), To: e.GetProperty("to").GetString()))
            .ToList();
        edges.Should().Contain(("change-b", "change-a"));
    }

    [Test]
    public async Task Dependencies_empty_or_missing_render_as_standalone_nodes()
    {
        WriteChange("solo", Frontmatter());          // depends-on: []
        WriteChange("bare", dependenciesMd: null);    // no dependencies.md

        var exit = await RunAsync("openspec", "dependencies", "--json");
        exit.Should().Be(0);

        var root = JsonDocument.Parse(Console.Output).RootElement;
        root.GetProperty("nodes").EnumerateArray().Select(e => e.GetString())
            .Should().Contain(["solo", "bare"]);
        root.GetProperty("edges").GetArrayLength().Should().Be(0);
    }

    [Test]
    public async Task Dependencies_ignores_soft_deps_in_html_comments()
    {
        WriteChange("change-a", dependenciesMd: null);
        WriteChange("change-b", "---\ndepends-on: []\n<!-- depends-on: [change-a] -->\n---\n");

        var exit = await RunAsync("openspec", "dependencies", "--json");
        exit.Should().Be(0);

        var root = JsonDocument.Parse(Console.Output).RootElement;
        root.GetProperty("edges").GetArrayLength().Should().Be(0);
    }

    [Test]
    public async Task Dependencies_warns_on_circular_dependencies()
    {
        WriteChange("a", Frontmatter("b"));
        WriteChange("b", Frontmatter("a"));

        var exit = await RunAsync("openspec", "dependencies");
        exit.Should().Be(0);
        Console.Output.Should().Contain("circular");
    }

    [Test]
    public async Task Dependencies_renders_graph_when_acyclic()
    {
        WriteChange("change-a", dependenciesMd: null);
        WriteChange("change-b", Frontmatter("change-a"));

        var exit = await RunAsync("openspec", "dependencies");
        exit.Should().Be(0);
        Console.Output.Should().Contain("change-a");
        Console.Output.Should().Contain("change-b");
    }
}
