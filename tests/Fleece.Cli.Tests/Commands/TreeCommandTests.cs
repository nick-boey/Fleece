using Fleece.Cli.Commands;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Search;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Tests.Commands;

[TestFixture]
public class TreeCommandTests
{
    private IFleeceService _fleeceService = null!;
    private ISettingsService _settingsService = null!;
    private ListCommand _command = null!;
    private CommandContext _context = null!;
    private StringWriter _consoleOutput = null!;
    private TextWriter _originalConsole = null!;
    private IAnsiConsole _originalAnsiConsole = null!;

    [SetUp]
    public void SetUp()
    {
        _fleeceService = Substitute.For<IFleeceService>();
        _fleeceService.GetSyncStatusesAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, SyncStatus>());
        _fleeceService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((false, string.Empty));
        _fleeceService.LoadIssuesWithDiagnosticsAsync(Arg.Any<CancellationToken>())
            .Returns(new LoadIssuesResult());

        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.GetEffectiveSettingsAsync(Arg.Any<FleeceSettings?>(), Arg.Any<CancellationToken>())
            .Returns(new EffectiveSettings
            {
                AutoMerge = false,
                Identity = "testuser",
                SyncBranch = null,
                Sources = new SettingsSources
                {
                    AutoMerge = SettingSource.Default,
                    Identity = SettingSource.Default,
                    SyncBranch = SettingSource.Default
                }
            });

        _command = new ListCommand(_fleeceService, _settingsService);
        _context = new CommandContext([], Substitute.For<IRemainingArguments>(), "list", null);

        _originalConsole = Console.Out;
        _originalAnsiConsole = AnsiConsole.Console;
        _consoleOutput = new StringWriter();
        Console.SetOut(_consoleOutput);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(_consoleOutput)
        });
    }

    [TearDown]
    public void TearDown()
    {
        AnsiConsole.Console = _originalAnsiConsole;
        Console.SetOut(_originalConsole);
        _consoleOutput.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_TreeOutput_UsesSharedIssueLineFormatter()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .WithPriority(1)
            .Build();

        var child = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Task")
            .WithStatus(IssueStatus.Progress)
            .WithType(IssueType.Bug)
            .WithPriority(2)
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var allIssues = new List<Issue> { parent, child };

        _fleeceService.FilterAsync(
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(allIssues);

        var settings = new ListSettings { Tree = true, All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString();

        // Tree should use the shared IssueLineFormatter format.
        output.Should().Contain("parent1");
        output.Should().Contain("[task]");
        output.Should().Contain("[open]");
        output.Should().Contain("P1");
        output.Should().Contain("Parent Task");

        output.Should().Contain("child1");
        output.Should().Contain("[bug]");
        output.Should().Contain("[progress]");
        output.Should().Contain("P2");
        output.Should().Contain("Child Task");
    }

    [Test]
    public async Task ExecuteAsync_TreeOutput_FormatsConsistentlyWithIssueLineFormatter()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Test Issue")
            .WithStatus(IssueStatus.Review)
            .WithType(IssueType.Feature)
            .WithPriority(3)
            .Build();

        _fleeceService.FilterAsync(
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { issue });

        var settings = new ListSettings { Tree = true, All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString();

        output.Should().Contain("abc123");
        output.Should().Contain("[feature]");
        output.Should().Contain("[review]");
        output.Should().Contain("P3");
        output.Should().Contain("Test Issue");
    }

    [Test]
    public async Task ExecuteAsync_TreeOutput_ShowsChildrenUnderParent()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Second")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        _fleeceService.FilterAsync(
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent, child1, child2 });

        var settings = new ListSettings { Tree = true, All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString();

        // All issues should appear in output
        output.Should().Contain("parent1");
        output.Should().Contain("child1");
        output.Should().Contain("child2");

        // Tree structure characters should be present
        output.Should().ContainAny("├", "└");
    }

    [Test]
    public async Task ExecuteAsync_NextMode_RendersTaskGraph()
    {
        var issue = new IssueBuilder()
            .WithId("task1")
            .WithTitle("A task")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .Build();

        _fleeceService.BuildTaskGraphLayoutAsync(Arg.Any<InactiveVisibility>(), Arg.Any<string?>(), Arg.Any<GraphSortConfig?>(), Arg.Any<CancellationToken>())
            .Returns(new TaskGraph
            {
                Nodes = [new TaskGraphNode { Issue = issue, Row = 0, Lane = 0, IsActionable = true }],
                TotalLanes = 1
            });

        var settings = new ListSettings { Next = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString();
        output.Should().Contain("task1");
        output.Should().Contain("A task");
    }

    [Test]
    public async Task ExecuteAsync_TreeAndNextMutuallyExclusive_ReturnsError()
    {
        var settings = new ListSettings { Tree = true, Next = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(1);
        _consoleOutput.ToString().Should().Contain("--tree and --next cannot be used together");
    }

    #region Search Integration Tests

    [Test]
    public async Task ExecuteAsync_NextWithSearch_CallsSearchServiceAndBuildsFilteredGraph()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Login bug")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Bug)
            .Build();

        var matchedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "abc123" };
        var searchResult = new SearchResult
        {
            MatchedIssues = new List<Issue> { issue },
            MatchedIds = matchedIds,
            ContextIssues = new List<Issue>()
        };

        var query = new SearchQuery { Tokens = [] };
        _fleeceService.ParseSearchQuery("login").Returns(query);
        _fleeceService.SearchWithContextAsync(
                query,
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var taskGraph = new TaskGraph
        {
            Nodes = new List<TaskGraphNode>
            {
                new() { Issue = issue, Row = 0, Lane = 0, IsActionable = true }
            },
            TotalLanes = 1,
            MatchedIds = matchedIds
        };

        _fleeceService.BuildFilteredTaskGraphLayoutAsync(matchedIds, Arg.Any<GraphSortConfig?>(), Arg.Any<CancellationToken>())
            .Returns(taskGraph);

        var settings = new ListSettings { Next = true, Search = "login" };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        // Verify search was called
        _fleeceService.Received(1).ParseSearchQuery("login");
        await _fleeceService.Received(1).SearchWithContextAsync(
            query,
            Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());

        // Verify filtered graph was built
        await _fleeceService.Received(1).BuildFilteredTaskGraphLayoutAsync(matchedIds, Arg.Any<GraphSortConfig?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_NextWithSearchNoResults_ShowsNoIssuesMessage()
    {
        var searchResult = new SearchResult
        {
            MatchedIssues = new List<Issue>(),
            MatchedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ContextIssues = new List<Issue>()
        };

        var query = new SearchQuery { Tokens = [] };
        _fleeceService.ParseSearchQuery("nonexistent").Returns(query);
        _fleeceService.SearchWithContextAsync(
                query,
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var settings = new ListSettings { Next = true, Search = "nonexistent" };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString();
        output.Should().Contain("No issues found matching search");
    }

    [Test]
    public async Task ExecuteAsync_NextWithSearch_PassesCliFiltersToSearchService()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Open bug")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Bug)
            .Build();

        var matchedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "abc123" };
        var searchResult = new SearchResult
        {
            MatchedIssues = new List<Issue> { issue },
            MatchedIds = matchedIds,
            ContextIssues = new List<Issue>()
        };

        var query = new SearchQuery { Tokens = [] };
        _fleeceService.ParseSearchQuery("type:bug").Returns(query);
        _fleeceService.SearchWithContextAsync(
                query,
                IssueStatus.Open,  // CLI status filter
                Arg.Any<IssueType?>(),
                Arg.Any<int?>(),
                Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<string>?>(),
                Arg.Any<int?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var taskGraph = new TaskGraph
        {
            Nodes = new List<TaskGraphNode>
            {
                new() { Issue = issue, Row = 0, Lane = 0, IsActionable = true }
            },
            TotalLanes = 1,
            MatchedIds = matchedIds
        };

        _fleeceService.BuildFilteredTaskGraphLayoutAsync(matchedIds, Arg.Any<GraphSortConfig?>(), Arg.Any<CancellationToken>())
            .Returns(taskGraph);

        var settings = new ListSettings { Next = true, Search = "type:bug", Status = "open" };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        // Verify search received CLI status filter
        await _fleeceService.Received(1).SearchWithContextAsync(
            query,
            IssueStatus.Open,  // CLI status passed through
            Arg.Any<IssueType?>(),
            Arg.Any<int?>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<int?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_NextWithoutSearch_CallsBuildTaskGraphLayoutAsync()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Any issue")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .Build();

        var taskGraph = new TaskGraph
        {
            Nodes = new List<TaskGraphNode>
            {
                new() { Issue = issue, Row = 0, Lane = 0, IsActionable = true }
            },
            TotalLanes = 1
        };

        _fleeceService.BuildTaskGraphLayoutAsync(Arg.Any<InactiveVisibility>(), Arg.Any<string?>(), Arg.Any<GraphSortConfig?>(), Arg.Any<CancellationToken>())
            .Returns(taskGraph);

        var settings = new ListSettings { Next = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        // Verify BuildTaskGraphLayoutAsync was called (not BuildFilteredTaskGraphLayoutAsync)
        await _fleeceService.Received(1).BuildTaskGraphLayoutAsync(Arg.Any<InactiveVisibility>(), Arg.Any<string?>(), Arg.Any<GraphSortConfig?>(), Arg.Any<CancellationToken>());

        // Verify search was NOT called
        _fleeceService.DidNotReceive().ParseSearchQuery(Arg.Any<string?>());
    }

    [Test]
    public async Task ExecuteAsync_NextWithSearch_OutputsMatchingIssue()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Login bug fix")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Bug)
            .Build();

        var matchedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "abc123" };
        var searchResult = new SearchResult
        {
            MatchedIssues = new List<Issue> { issue },
            MatchedIds = matchedIds,
            ContextIssues = new List<Issue>()
        };

        var query = new SearchQuery { Tokens = [] };
        _fleeceService.ParseSearchQuery("login").Returns(query);
        _fleeceService.SearchWithContextAsync(
                query,
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var taskGraph = new TaskGraph
        {
            Nodes = new List<TaskGraphNode>
            {
                new() { Issue = issue, Row = 0, Lane = 0, IsActionable = true }
            },
            TotalLanes = 1,
            MatchedIds = matchedIds
        };

        _fleeceService.BuildFilteredTaskGraphLayoutAsync(matchedIds, Arg.Any<GraphSortConfig?>(), Arg.Any<CancellationToken>())
            .Returns(taskGraph);

        var settings = new ListSettings { Next = true, Search = "login" };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString();
        output.Should().Contain("abc123");
        output.Should().Contain("Login bug fix");
    }

    #endregion
}
