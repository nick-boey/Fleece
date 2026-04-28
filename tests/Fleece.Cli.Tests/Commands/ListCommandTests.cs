using Fleece.Cli.Commands;
using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Search;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Fleece.Cli.Tests.Commands;

[TestFixture]
public class ListCommandTests
{
    private IFleeceService _fleeceService = null!;
    private IIssueLayoutService _issueLayoutService = null!;
    private ISettingsService _settingsService = null!;
    private ListCommand _command = null!;
    private CommandContext _context = null!;
    private TestConsole _console = null!;
    private StringWriter _consoleOutput = null!;
    private TextWriter _originalConsole = null!;

    [SetUp]
    public void SetUp()
    {
        _fleeceService = Substitute.For<IFleeceService>();
        _fleeceService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((false, string.Empty));
        _fleeceService.LoadIssuesWithDiagnosticsAsync(Arg.Any<CancellationToken>())
            .Returns(new LoadIssuesResult());

        _fleeceService.GetSyncStatusesAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, SyncStatus>());

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

        _issueLayoutService = Substitute.For<IIssueLayoutService>();

        _console = new TestConsole();
        _command = new ListCommand(_fleeceService, _issueLayoutService, _settingsService, _console);
        _context = new CommandContext([], Substitute.For<IRemainingArguments>(), "list", null);

        _originalConsole = Console.Out;
        _consoleOutput = new StringWriter();
        Console.SetOut(_consoleOutput);
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetOut(_originalConsole);
        _consoleOutput.Dispose();
        _console.Dispose();
    }

    private string CombinedOutput() => _console.Output + _consoleOutput.ToString();

    [Test]
    public async Task ExecuteAsync_OneLine_UsesSharedIssueLineFormatterPlainText()
    {
        var issue1 = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("First issue")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .Build();

        var issue2 = new IssueBuilder()
            .WithId("def456")
            .WithTitle("Second issue")
            .WithStatus(IssueStatus.Progress)
            .WithType(IssueType.Bug)
            .Build();

        _fleeceService.FilterAsync(
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { issue1, issue2 });

        var settings = new ListSettings { OneLine = true, All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = CombinedOutput();

        var expectedLine1 = IssueLineFormatter.FormatPlainText(issue1);
        var expectedLine2 = IssueLineFormatter.FormatPlainText(issue2);

        output.Should().Contain(expectedLine1);
        output.Should().Contain(expectedLine2);

        output.Should().Contain("abc123 open task First issue");
        output.Should().Contain("def456 progress bug Second issue");
    }

    [Test]
    public async Task ExecuteAsync_OneLine_LowercasesStatusAndType()
    {
        var issue = new IssueBuilder()
            .WithId("xyz789")
            .WithTitle("Review issue")
            .WithStatus(IssueStatus.Review)
            .WithType(IssueType.Feature)
            .Build();

        _fleeceService.FilterAsync(
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { issue });

        var settings = new ListSettings { OneLine = true, All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        CombinedOutput().Should().Contain("xyz789 review feature Review issue");
    }

    [Test]
    public async Task ExecuteAsync_OneLine_OutputMatchesFormatterExactly()
    {
        var issue = new IssueBuilder()
            .WithId("test01")
            .WithTitle("My Task")
            .WithStatus(IssueStatus.Complete)
            .WithType(IssueType.Chore)
            .Build();

        _fleeceService.FilterAsync(
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { issue });

        var settings = new ListSettings { OneLine = true, All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = CombinedOutput().Trim();
        var expected = IssueLineFormatter.FormatPlainText(issue);

        output.Should().Be(expected);
    }

    [Test]
    public async Task ExecuteAsync_OneLine_EmptyList_ShowsNoIssuesMessage()
    {
        _fleeceService.FilterAsync(
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Issue>());

        var settings = new ListSettings { OneLine = true, All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        CombinedOutput().Should().Contain("No issues found");
    }

    #region Search Integration Tests

    [Test]
    public async Task ExecuteAsync_WithSearch_CallsSearchService()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Login bug")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Bug)
            .Build();

        var query = new SearchQuery { Tokens = [] };
        _fleeceService.ParseSearchQuery("login").Returns(query);
        _fleeceService.SearchWithFiltersAsync(
                query,
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { issue });

        var settings = new ListSettings { Search = "login", OneLine = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        _fleeceService.Received(1).ParseSearchQuery("login");
        await _fleeceService.Received(1).SearchWithFiltersAsync(
            query,
            Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());

        await _fleeceService.DidNotReceive().FilterAsync(
            Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_SearchCombinedWithCliFlag_PassesBothToSearchService()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Open bug")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Bug)
            .Build();

        var query = new SearchQuery { Tokens = [] };
        _fleeceService.ParseSearchQuery("type:bug").Returns(query);
        _fleeceService.SearchWithFiltersAsync(
                query,
                IssueStatus.Open,
                Arg.Any<IssueType?>(),
                Arg.Any<int?>(),
                Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<string>?>(),
                Arg.Any<int?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { issue });

        var settings = new ListSettings { Search = "type:bug", Status = "open", OneLine = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        await _fleeceService.Received(1).SearchWithFiltersAsync(
            query,
            IssueStatus.Open,
            Arg.Any<IssueType?>(),
            Arg.Any<int?>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<int?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_SearchOneLine_OutputsMatchingIssues()
    {
        var issue1 = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Login issue")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Bug)
            .Build();

        var issue2 = new IssueBuilder()
            .WithId("def456")
            .WithTitle("Login page")
            .WithStatus(IssueStatus.Progress)
            .WithType(IssueType.Feature)
            .Build();

        var query = new SearchQuery { Tokens = [] };
        _fleeceService.ParseSearchQuery("login").Returns(query);
        _fleeceService.SearchWithFiltersAsync(
                query,
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { issue1, issue2 });

        var settings = new ListSettings { Search = "login", OneLine = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = CombinedOutput();
        output.Should().Contain("abc123 open bug Login issue");
        output.Should().Contain("def456 progress feature Login page");
    }

    [Test]
    public async Task ExecuteAsync_SearchNoResults_ShowsNoIssuesMessage()
    {
        var query = new SearchQuery { Tokens = [] };
        _fleeceService.ParseSearchQuery("nonexistent").Returns(query);
        _fleeceService.SearchWithFiltersAsync(
                query,
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<Issue>());

        var settings = new ListSettings { Search = "nonexistent", OneLine = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        CombinedOutput().Should().Contain("No issues found");
    }

    [Test]
    public async Task ExecuteAsync_WithoutSearch_UsesFilterAsync()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Any issue")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .Build();

        _fleeceService.FilterAsync(
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { issue });

        var settings = new ListSettings { OneLine = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        await _fleeceService.Received(1).FilterAsync(
            Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());

        _fleeceService.DidNotReceive().ParseSearchQuery(Arg.Any<string?>());
    }

    #endregion
}
