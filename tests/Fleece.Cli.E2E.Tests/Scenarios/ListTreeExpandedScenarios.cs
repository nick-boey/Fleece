namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("list")]
public class ListTreeExpandedScenarios : CliScenarioTestBase
{
    private string _capturedListOutput = string.Empty;

    private async Task<string> CreateAsync(
        string title,
        string? description = null,
        string? parentArg = null,
        string? executionMode = null,
        string? status = null)
    {
        var args = new List<string> { "create", "-t", title, "-y", "task" };
        if (description is not null)
        {
            args.AddRange(new[] { "-d", description });
        }
        if (parentArg is not null)
        {
            args.AddRange(new[] { "--parent-issues", parentArg });
        }
        if (executionMode is not null)
        {
            args.AddRange(new[] { "-e", executionMode });
        }
        if (status is not null)
        {
            args.AddRange(new[] { "-s", status });
        }

        var existingIds = LoadIssues().Select(i => i.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        await RunAsync(args.ToArray());
        var newIssue = LoadIssues().Single(i => !existingIds.Contains(i.Id) && i.Title == title);
        return newIssue.Id;
    }

    private async Task<int> RunListAsync(params string[] args)
    {
        var beforeLength = Console.Output.Length;
        var exit = await RunAsync(args);
        _capturedListOutput = Console.Output[beforeLength..];
        return exit;
    }

    private SettingsTask VerifyListOutputWithIdScrubbing()
    {
        var idsInOrder = LoadIssues().Select(i => i.Id).ToList();

        return Verifier.Verify(_capturedListOutput)
            .AddScrubber(builder =>
            {
                for (int i = 0; i < idsInOrder.Count; i++)
                {
                    builder.Replace(idsInOrder[i], $"<id-{i + 1}>");
                }
            });
    }

    [Test]
    public async Task Tree_expanded_series_chain_renders()
    {
        var rootId = await CreateAsync("Root", "root", executionMode: "series");
        var midId = await CreateAsync("Middle", "mid", parentArg: rootId);
        await CreateAsync("Leaf", "leaf", parentArg: midId);

        var exit = await RunListAsync("list", "--tree", "--expanded");
        exit.Should().Be(0);

        await VerifyListOutputWithIdScrubbing();
    }

    [Test]
    public async Task Tree_expanded_parallel_fan_renders()
    {
        var rootId = await CreateAsync("Parallel Root", "root", executionMode: "parallel");
        await CreateAsync("Child A", "a", parentArg: rootId);
        await CreateAsync("Child B", "b", parentArg: rootId);
        await CreateAsync("Child C", "c", parentArg: rootId);

        var exit = await RunListAsync("list", "--tree", "--expanded");
        exit.Should().Be(0);

        await VerifyListOutputWithIdScrubbing();
    }

    [Test]
    public async Task Tree_expanded_mixed_depth_renders()
    {
        var rootId = await CreateAsync("Mixed Root", "root", executionMode: "series");
        var seriesChildId = await CreateAsync("Series Child", "sc", parentArg: rootId);
        await CreateAsync("Series Grand", "sg", parentArg: seriesChildId);
        var parallelChildId = await CreateAsync("Parallel Child", "pc", parentArg: rootId, executionMode: "parallel");
        await CreateAsync("Parallel Grand A", "pga", parentArg: parallelChildId);
        await CreateAsync("Parallel Grand B", "pgb", parentArg: parallelChildId);

        var exit = await RunListAsync("list", "--tree", "--expanded");
        exit.Should().Be(0);

        await VerifyListOutputWithIdScrubbing();
    }

    [Test]
    public async Task Tree_expanded_multi_parent_fan_in_renders()
    {
        var parentAId = await CreateAsync("Parent A", "pa", executionMode: "series");
        var parentBId = await CreateAsync("Parent B", "pb", executionMode: "series");
        await CreateAsync("Shared Child", "shared", parentArg: $"{parentAId},{parentBId}");

        var exit = await RunListAsync("list", "--tree", "--expanded");
        exit.Should().Be(0);

        await VerifyListOutputWithIdScrubbing();
    }

    [Test]
    public async Task Tree_expanded_parallel_children_each_have_fan_glyph()
    {
        var rootId = await CreateAsync("Parallel Root", "root", executionMode: "parallel", status: "open");
        await CreateAsync("Child A", "a", parentArg: $"{rootId}:aaa", status: "open");
        await CreateAsync("Child B", "b", parentArg: $"{rootId}:aab", status: "open");
        await CreateAsync("Child C", "c", parentArg: $"{rootId}:aac", status: "open");

        var exit = await RunListAsync("list", "--tree", "--expanded");
        exit.Should().Be(0);

        var childLines = _capturedListOutput.Split('\n')
            .Where(l => l.Contains("Child"))
            .ToList();
        childLines.Should().HaveCount(3);

        // Each parallel child must have a fan glyph (├ or └) attaching it to the
        // parent spine; otherwise parallel renders as a series-style vertical chain
        // and the user can't tell parallel and series apart.
        foreach (var line in childLines)
        {
            (line.Contains('├') || line.Contains('└')).Should().BeTrue(
                $"each parallel child should have a fan connector glyph; got: {line}");
        }
    }

    [Test]
    public async Task Tree_expanded_series_and_parallel_fan_have_different_child_glyphs()
    {
        var seriesRoot = await CreateAsync("Series Root", "sr", executionMode: "series", status: "open");
        await CreateAsync("Series A", "sa", parentArg: $"{seriesRoot}:aaa", status: "open");
        await CreateAsync("Series B", "sb", parentArg: $"{seriesRoot}:aab", status: "open");
        await CreateAsync("Series C", "sc", parentArg: $"{seriesRoot}:aac", status: "open");

        var exit = await RunListAsync("list", "--tree", "--expanded", seriesRoot);
        exit.Should().Be(0);
        var seriesChildLines = _capturedListOutput.Split('\n')
            .Where(l => l.Contains("Series A") || l.Contains("Series B") || l.Contains("Series C"))
            .Select(l => l[..l.IndexOfAny(new[] { '○', '◌', '●', '⊘' })])
            .ToList();

        var parallelRoot = await CreateAsync("Parallel Root", "pr", executionMode: "parallel", status: "open");
        await CreateAsync("Parallel A", "pa", parentArg: $"{parallelRoot}:aaa", status: "open");
        await CreateAsync("Parallel B", "pb", parentArg: $"{parallelRoot}:aab", status: "open");
        await CreateAsync("Parallel C", "pc", parentArg: $"{parallelRoot}:aac", status: "open");

        exit = await RunListAsync("list", "--tree", "--expanded", parallelRoot);
        exit.Should().Be(0);
        var parallelChildLines = _capturedListOutput.Split('\n')
            .Where(l => l.Contains("Parallel A") || l.Contains("Parallel B") || l.Contains("Parallel C"))
            .Select(l => l[..l.IndexOfAny(new[] { '○', '◌', '●', '⊘' })])
            .ToList();

        seriesChildLines.Should().HaveCount(3);
        parallelChildLines.Should().HaveCount(3);

        // The prefix of each child line (everything before the node marker) must
        // differ between series and parallel for at least one child; otherwise the
        // execution mode is invisible from the rendered child rows.
        seriesChildLines.Should().NotEqual(parallelChildLines,
            "series-fan and parallel-fan must render their children with different connector glyphs");
    }

    [Test]
    public async Task Tree_expanded_terminal_with_active_descendants_renders()
    {
        var doneParentId = await CreateAsync("Done Parent", "p", executionMode: "series", status: "complete");
        await CreateAsync("Active Child", "c", parentArg: doneParentId);

        var exit = await RunListAsync("list", "--tree", "--expanded", "--show-inactive", "if-active-children");
        exit.Should().Be(0);

        await VerifyListOutputWithIdScrubbing();
    }

    [Test]
    public async Task Tree_compact_unchanged_when_expanded_omitted()
    {
        var rootId = await CreateAsync("Root", "root", executionMode: "series");
        var midId = await CreateAsync("Middle", "mid", parentArg: rootId);
        await CreateAsync("Leaf", "leaf", parentArg: midId);

        var exit = await RunListAsync("list", "--tree");
        exit.Should().Be(0);

        // The compact tree renderer prints one issue per line, with the ASCII tree
        // glyphs (└──, ├──) on the left and the title on the right. The engine path
        // would produce two-row-per-issue spacing with markers (○ ◌); assert the
        // compact shape contains all titles and no graph node markers.
        var lines = _capturedListOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.Contains("Root") || l.Contains("Middle") || l.Contains("Leaf"))
            .ToList();
        lines.Should().HaveCount(3);
        _capturedListOutput.Should().NotContain("◌");
        _capturedListOutput.Should().NotContain("○");
    }

    [Test]
    public async Task Expanded_alone_errors()
    {
        await CreateAsync("Lonely", "x");

        var exit = await RunAsync("list", "--expanded");
        exit.Should().Be(1);
        Console.Output.Should().Contain("--expanded requires --tree");
    }

    [Test]
    public async Task Next_expanded_errors()
    {
        await CreateAsync("Lonely", "x");

        var exit = await RunAsync("list", "--next", "--expanded");
        exit.Should().Be(1);
        Console.Output.Should().Contain("--expanded cannot be used with --next");
    }

    [Test]
    public async Task Tree_expanded_json_errors()
    {
        await CreateAsync("Lonely", "x");

        var exit = await RunAsync("list", "--tree", "--expanded", "--json");
        exit.Should().Be(1);
        Console.Output.Should().Contain("--tree --expanded cannot be used with --json");
    }
}
