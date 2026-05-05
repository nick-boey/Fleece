using Fleece.Cli.Commands;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Fleece.Cli.Tests.Commands;

[TestFixture]
public class ShowCommandTests
{
    private IFleeceService _fleeceService = null!;
    private ISettingsService _settingsService = null!;
    private IGitConfigService _gitConfigService = null!;
    private ShowCommand _command = null!;
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

        _settingsService = Substitute.For<ISettingsService>();
        _gitConfigService = Substitute.For<IGitConfigService>();
        _console = new TestConsole();

        _command = new ShowCommand(_fleeceService, _settingsService, _gitConfigService, _console);
        _context = new CommandContext([], Substitute.For<IRemainingArguments>(), "show", null);

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
    public async Task ExecuteAsync_WithSeriesParent_TableOutputShowsPositionAndPrevNext()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First Child")
            .WithStatus(IssueStatus.Complete)
            .WithType(IssueType.Task)
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Second Child")
            .WithStatus(IssueStatus.Progress)
            .WithType(IssueType.Bug)
            .WithPriority(1)
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var child3 = new IssueBuilder()
            .WithId("child3")
            .WithTitle("Third Child")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Feature)
            .WithParentIssueIdAndOrder("parent1", "aac")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2, child3 };

        _fleeceService.ResolveByPartialIdAsync("child2", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child2 });
        _fleeceService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(allIssues);

        var settings = new ShowSettings { Id = "child2" };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = CombinedOutput();

        output.Should().Contain("parent1");
        output.Should().Contain("Parent Task");
        output.Should().Contain("series");
        output.Should().Contain("2 of 3");

        output.Should().Contain("child1");
        output.Should().Contain("First Child");
        output.Should().Contain("child3");
        output.Should().Contain("Third Child");
    }

    [Test]
    public async Task ExecuteAsync_WithJson_OutputIncludesHierarchyContext()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Task")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var allIssues = new List<Issue> { parent, child };

        _fleeceService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });
        _fleeceService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(allIssues);

        var settings = new ShowSettings { Id = "child1", Json = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString().Trim();

        var doc = System.Text.Json.JsonDocument.Parse(output);
        var root = doc.RootElement;

        root.GetProperty("issue").GetProperty("id").GetString().Should().Be("child1");
        root.GetProperty("parents").GetArrayLength().Should().Be(1);
        root.GetProperty("parents")[0].GetProperty("parent")
            .GetProperty("id").GetString().Should().Be("parent1");
        root.GetProperty("parents")[0].GetProperty("executionMode")
            .GetString().Should().Be("Series");
    }

    [Test]
    public async Task ExecuteAsync_WithJsonAndChildren_OutputIncludesChildList()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First Child")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Second Child")
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2 };

        _fleeceService.ResolveByPartialIdAsync("parent1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent });
        _fleeceService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(allIssues);

        var settings = new ShowSettings { Id = "parent1", Json = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString().Trim();
        var doc = System.Text.Json.JsonDocument.Parse(output);
        var root = doc.RootElement;

        root.GetProperty("children").GetArrayLength().Should().Be(2);
        root.GetProperty("children")[0].GetProperty("id").GetString().Should().Be("child1");
        root.GetProperty("children")[1].GetProperty("id").GetString().Should().Be("child2");
        root.GetProperty("executionMode").GetString().Should().Be("Series");
    }

    [Test]
    public async Task ExecuteAsync_WithJsonVerbose_OutputsRawIssueWithoutHierarchy()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Task")
            .WithStatus(IssueStatus.Progress)
            .WithType(IssueType.Feature)
            .WithPriority(2)
            .WithDescription("Detailed description")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        _fleeceService.ResolveByPartialIdAsync("child1", Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { child });

        var settings = new ShowSettings { Id = "child1", JsonVerbose = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString().Trim();
        var doc = System.Text.Json.JsonDocument.Parse(output);
        var root = doc.RootElement;

        root.GetProperty("id").GetString().Should().Be("child1");
        root.GetProperty("title").GetString().Should().Be("Child Task");
        root.GetProperty("status").GetString().Should().Be("Progress");
        root.GetProperty("type").GetString().Should().Be("Feature");
        root.GetProperty("priority").GetInt32().Should().Be(2);
        root.GetProperty("description").GetString().Should().Be("Detailed description");

        root.TryGetProperty("createdAt", out _).Should().BeTrue();
        root.TryGetProperty("lastUpdate", out _).Should().BeTrue();

        root.TryGetProperty("parents", out _).Should().BeFalse();
        root.TryGetProperty("children", out _).Should().BeFalse();

        root.TryGetProperty("parentIssues", out var parentIssues).Should().BeTrue();
        parentIssues.GetArrayLength().Should().Be(1);

        await _fleeceService.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());
    }
}
