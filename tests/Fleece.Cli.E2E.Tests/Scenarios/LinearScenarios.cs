using System.Text.Json;
using Fleece.Core.Models;

namespace Fleece.Cli.E2E.Tests.Scenarios;

/// <summary>
/// Exercises the Linear durable-tracker command surfaces (promote/absorb/auth). Linear is
/// agent-realized: the CLI makes no network calls, so every scenario runs with the plain
/// <see cref="CliScenarioTestBase.RunAsync"/> — no fake GitHub service is registered or required.
/// </summary>
[TestFixture]
[Category("promote")]
[Category("absorb")]
[Category("auth")]
[Category("linear")]
public class LinearScenarios : CliScenarioTestBase
{
    private async Task SetLinearTrackerAsync()
        => (await RunAsync("config", "--set", "tracker=linear")).Should().Be(0);

    private async Task<string> CreateIssueAsync(string title, string description = "body")
    {
        await RunAsync("create", "-t", title, "-y", "task", "-d", description);
        return LoadIssues().Single(i => i.Title == title).Id;
    }

    [Test]
    public async Task Promote_bare_emits_payload_and_changes_no_state()
    {
        await SetLinearTrackerAsync();
        var a = await CreateIssueAsync("Root work", "Do the root thing.");
        var b = await CreateIssueAsync("Child work", "Do the child thing.");
        Console.Clear(home: true);

        var exit = await RunAsync("promote", a, b);

        exit.Should().Be(0);
        // The emitted payload carries the root title and the exact --ref re-run command.
        Console.Output.Should().Contain("Root work");
        Console.Output.Should().Contain("--ref");
        Console.Output.Should().Contain(a).And.Contain(b);

        // Emit changes NO state.
        var issues = LoadIssues();
        var issueA = issues.Single(i => i.Id == a);
        issueA.Status.Should().Be(IssueStatus.Open);
        KeyedTag.HasKey(issueA.Tags, "promoted").Should().BeFalse();
        issues.Single(i => i.Id == b).Status.Should().Be(IssueStatus.Open);
    }

    [Test]
    public async Task Promote_bare_json_emits_title_body_and_issue_ids()
    {
        await SetLinearTrackerAsync();
        var a = await CreateIssueAsync("JSON root", "desc a");
        var b = await CreateIssueAsync("JSON child", "desc b");
        Console.Clear(home: true);

        var exit = await RunAsync("promote", a, b, "--json");

        exit.Should().Be(0);
        // Assert on the JSON payload keys/values via substrings (the injected console word-wraps at
        // 80 cols, so parsing a long JSON line is unreliable).
        Console.Output.Should().Contain("\"title\"");
        Console.Output.Should().Contain("\"body\"");
        Console.Output.Should().Contain("\"issueIds\"");
        Console.Output.Should().Contain(a).And.Contain(b);

        // No state change on a JSON emit either.
        LoadIssues().Single(i => i.Id == a).Status.Should().Be(IssueStatus.Open);
    }

    [Test]
    public async Task Promote_with_ref_records_reference_and_sets_promoted()
    {
        await SetLinearTrackerAsync();
        var a = await CreateIssueAsync("Recorded");
        Console.Clear(home: true);

        var exit = await RunAsync("promote", a, "--ref", "ENG-42");

        exit.Should().Be(0);
        var issue = LoadIssues().Single(i => i.Id == a);
        issue.Status.Should().Be(IssueStatus.Promoted);
        KeyedTag.GetValues(issue.Tags, "promoted").Should().Contain("ENG-42");
    }

    [Test]
    public async Task Promote_with_ref_accepts_a_url_style_reference()
    {
        await SetLinearTrackerAsync();
        var a = await CreateIssueAsync("Url ref");
        Console.Clear(home: true);

        var exit = await RunAsync("promote", a, "--ref", "https://linear.app/acme/issue/ENG-7");

        exit.Should().Be(0);
        var issue = LoadIssues().Single(i => i.Id == a);
        issue.Status.Should().Be(IssueStatus.Promoted);
        KeyedTag.GetValues(issue.Tags, "promoted").Should().Contain("https://linear.app/acme/issue/ENG-7");
    }

    [Test]
    public async Task Promote_skips_already_promoted_issue_and_keeps_existing_ref()
    {
        await SetLinearTrackerAsync();
        var a = await CreateIssueAsync("Once");
        (await RunAsync("promote", a, "--ref", "ENG-1")).Should().Be(0);
        Console.Clear(home: true);

        var exit = await RunAsync("promote", a, "--ref", "ENG-2");

        exit.Should().Be(0);
        Console.Output.Should().Contain("already promoted");
        var tags = KeyedTag.GetValues(LoadIssues().Single(i => i.Id == a).Tags, "promoted").ToList();
        tags.Should().Contain("ENG-1");
        tags.Should().NotContain("ENG-2");
    }

    [Test]
    public async Task Absorb_linear_prints_guidance_and_makes_no_change()
    {
        await SetLinearTrackerAsync();
        Console.Clear(home: true);

        var exit = await RunAsync("absorb", "ENG-42");

        exit.Should().Be(0);
        Console.Output.Should().Contain("fleece create");
        Console.Output.Should().Contain("absorbed-from=ENG-42");
        // No Fleece issue is created.
        LoadIssues().Should().BeEmpty();
    }

    [Test]
    public async Task Auth_linear_reports_not_applicable_and_exits_zero()
    {
        await SetLinearTrackerAsync();
        Console.Clear(home: true);

        var exit = await RunAsync("auth");

        exit.Should().Be(0);
        Console.Output.Should().Contain("linear");
        Console.Output.Should().Contain("does not authenticate");
    }

    [Test]
    public async Task Auth_linear_json_carries_tracker_and_applicable_false()
    {
        await SetLinearTrackerAsync();
        // Slice off just the auth output; Console.Clear emits ANSI escape codes into the buffer
        // rather than truncating it, which would break a JSON parse of the whole buffer.
        var before = Console.Output.Length;

        var exit = await RunAsync("auth", "--json");

        exit.Should().Be(0);
        var json = JsonDocument.Parse(Console.Output[before..].Trim()).RootElement;
        json.GetProperty("tracker").GetString().Should().Be("linear");
        json.GetProperty("applicable").GetBoolean().Should().BeFalse();
    }
}
