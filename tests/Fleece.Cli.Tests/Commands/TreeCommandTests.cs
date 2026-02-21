using Fleece.Cli.Commands;
using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
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
    private IIssueService _issueService = null!;
    private IStorageService _storageService = null!;
    private IStorageServiceProvider _storageServiceProvider = null!;
    private IIssueServiceFactory _issueServiceFactory = null!;
    private IIssueGraphService _graphService = null!;
    private TreeCommand _command = null!;
    private CommandContext _context = null!;
    private StringWriter _consoleOutput = null!;
    private TextWriter _originalConsole = null!;
    private IAnsiConsole _originalAnsiConsole = null!;

    [SetUp]
    public void SetUp()
    {
        _issueService = Substitute.For<IIssueService>();
        _storageService = Substitute.For<IStorageService>();
        _graphService = Substitute.For<IIssueGraphService>();
        _storageService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((false, string.Empty));

        _storageServiceProvider = Substitute.For<IStorageServiceProvider>();
        _storageServiceProvider.GetStorageService(Arg.Any<string?>())
            .Returns(_storageService);

        _issueServiceFactory = Substitute.For<IIssueServiceFactory>();
        _issueServiceFactory.GetIssueService(Arg.Any<string?>())
            .Returns(_issueService);

        _command = new TreeCommand(_issueServiceFactory, _storageServiceProvider, _graphService);
        _context = new CommandContext([], Substitute.For<IRemainingArguments>(), "tree", null);

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

        _issueService.FilterAsync(
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(allIssues);

        var settings = new TreeSettings { All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString();

        // Tree should use the shared IssueLineFormatter format.
        // Verify the output contains the formatted issue lines matching
        // IssueLineFormatter.FormatMarkup output components:
        // {id} [{type}] [{status}] P{priority} {title}
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

        _issueService.FilterAsync(
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { issue });

        var settings = new TreeSettings { All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString();

        // The tree output for root issues should contain all the components
        // that IssueLineFormatter.FormatMarkup produces
        var expectedMarkup = IssueLineFormatter.FormatMarkup(issue);

        // Verify each component from the formatter appears in the tree output
        // (markup tags get rendered by Spectre.Console, so verify content portions)
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

        _issueService.FilterAsync(
                Arg.Any<IssueStatus?>(), Arg.Any<IssueType?>(), Arg.Any<int?>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<int?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<Issue> { parent, child1, child2 });

        var settings = new TreeSettings { All = true };

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
}
