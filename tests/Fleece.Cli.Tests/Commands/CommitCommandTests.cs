using Fleece.Cli.Commands;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Tests.Commands;

[TestFixture]
public class CommitCommandTests
{
    private IGitService _gitService = null!;
    private CommitCommand _command = null!;
    private CommandContext _context = null!;
    private StringWriter _consoleOutput = null!;
    private TextWriter _originalConsole = null!;
    private IAnsiConsole _originalAnsiConsole = null!;

    [SetUp]
    public void SetUp()
    {
        _gitService = Substitute.For<IGitService>();
        _gitService.IsGitAvailable().Returns(true);
        _gitService.IsGitRepository().Returns(true);
        _gitService.HasFleeceChanges().Returns(true);
        _gitService.CommitFleeceChanges(Arg.Any<string>()).Returns(GitOperationResult.Ok());

        _command = new CommitCommand(_gitService);
        _context = new CommandContext([], Substitute.For<IRemainingArguments>(), "commit", null);

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
        Console.SetOut(_originalConsole);
        AnsiConsole.Console = _originalAnsiConsole;
        _consoleOutput.Dispose();
    }

    [Test]
    public void Execute_AppendsSkipCi_ByDefault()
    {
        var settings = new CommitSettings();

        _command.Execute(_context, settings);

        _gitService.Received(1).CommitFleeceChanges("Update fleece issues [skip ci]");
    }

    [Test]
    public void Execute_AppendsSkipCi_ToCustomMessage()
    {
        var settings = new CommitSettings { Message = "My custom message" };

        _command.Execute(_context, settings);

        _gitService.Received(1).CommitFleeceChanges("My custom message [skip ci]");
    }

    [Test]
    public void Execute_OmitsSkipCi_WhenCiFlagSet()
    {
        var settings = new CommitSettings { Ci = true };

        _command.Execute(_context, settings);

        _gitService.Received(1).CommitFleeceChanges("Update fleece issues");
    }

    [Test]
    public void Execute_OmitsSkipCi_WhenCiFlagSetWithCustomMessage()
    {
        var settings = new CommitSettings { Message = "My custom message", Ci = true };

        _command.Execute(_context, settings);

        _gitService.Received(1).CommitFleeceChanges("My custom message");
    }
}
