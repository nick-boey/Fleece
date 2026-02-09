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
public class ListCommandTests
{
    private IStorageService _storageService = null!;
    private ListCommand _command = null!;
    private CommandContext _context = null!;
    private StringWriter _consoleOutput = null!;
    private TextWriter _originalConsole = null!;
    private IAnsiConsole _originalAnsiConsole = null!;

    [SetUp]
    public void SetUp()
    {
        _storageService = Substitute.For<IStorageService>();
        _storageService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((false, string.Empty));

        _command = new ListCommand(_storageService);
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

        _storageService.LoadIssuesWithDiagnosticsAsync(Arg.Any<CancellationToken>())
            .Returns(new LoadIssuesResult { Issues = new List<Issue> { issue1, issue2 } });

        var settings = new ListSettings { OneLine = true, All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString();

        // Verify the output matches IssueLineFormatter.FormatPlainText format:
        // {id} {status} {type} {title}
        var expectedLine1 = IssueLineFormatter.FormatPlainText(issue1);
        var expectedLine2 = IssueLineFormatter.FormatPlainText(issue2);

        output.Should().Contain(expectedLine1);
        output.Should().Contain(expectedLine2);

        // Verify the exact format
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

        _storageService.LoadIssuesWithDiagnosticsAsync(Arg.Any<CancellationToken>())
            .Returns(new LoadIssuesResult { Issues = new List<Issue> { issue } });

        var settings = new ListSettings { OneLine = true, All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString();

        output.Should().Contain("xyz789 review feature Review issue");
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

        _storageService.LoadIssuesWithDiagnosticsAsync(Arg.Any<CancellationToken>())
            .Returns(new LoadIssuesResult { Issues = new List<Issue> { issue } });

        var settings = new ListSettings { OneLine = true, All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString().Trim();
        var expected = IssueLineFormatter.FormatPlainText(issue);

        output.Should().Be(expected);
    }

    [Test]
    public async Task ExecuteAsync_OneLine_EmptyList_ShowsNoIssuesMessage()
    {
        _storageService.LoadIssuesWithDiagnosticsAsync(Arg.Any<CancellationToken>())
            .Returns(new LoadIssuesResult { Issues = new List<Issue>() });

        var settings = new ListSettings { OneLine = true, All = true };

        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);

        var output = _consoleOutput.ToString();

        output.Should().Contain("No issues found");
    }
}
