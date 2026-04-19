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
public class EditCommandTests
{
    private IFleeceService _fleeceService = null!;
    private ISettingsService _settingsService = null!;
    private IGitConfigService _gitConfigService = null!;
    private EditCommand _command = null!;
    private CommandContext _context = null!;
    private TestConsole _console = null!;

    [SetUp]
    public void SetUp()
    {
        _fleeceService = Substitute.For<IFleeceService>();
        _fleeceService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((false, string.Empty));

        var existing = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Existing")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .Build();

        _fleeceService.ResolveByPartialIdAsync("abc123", Arg.Any<CancellationToken>())
            .Returns(new[] { existing });

        _settingsService = Substitute.For<ISettingsService>();
        _gitConfigService = Substitute.For<IGitConfigService>();
        _console = new TestConsole();

        _command = new EditCommand(_fleeceService, _settingsService, _gitConfigService, _console);
        _context = new CommandContext([], Substitute.For<IRemainingArguments>(), "edit", null);
    }

    [TearDown]
    public void TearDown()
    {
        _console.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_IdOnly_ReturnsErrorListingFieldFlags()
    {
        var settings = new EditSettings { Id = "abc123" };

        var exitCode = await _command.ExecuteAsync(_context, settings);

        exitCode.Should().Be(1);
        var output = _console.Output;
        output.Should().Contain("at least one field flag");
        output.Should().Contain("--title");
        output.Should().Contain("--status");
        await _fleeceService.DidNotReceiveWithAnyArgs().UpdateAsync(id: Arg.Any<string>());
    }

    [Test]
    public async Task ExecuteAsync_IdWithOnlyJson_ReturnsErrorBecauseJsonIsNotAFieldUpdate()
    {
        var settings = new EditSettings { Id = "abc123", Json = true };

        var exitCode = await _command.ExecuteAsync(_context, settings);

        exitCode.Should().Be(1);
        _console.Output.Should().Contain("at least one field flag");
        await _fleeceService.DidNotReceiveWithAnyArgs().UpdateAsync(id: Arg.Any<string>());
    }

    [Test]
    public async Task ExecuteAsync_IdWithOnlyLinkedPr_InvokesUpdate()
    {
        var updated = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Existing")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .Build();
        _fleeceService.UpdateAsync(id: "abc123", linkedPr: 42)
            .ReturnsForAnyArgs(updated);

        var settings = new EditSettings { Id = "abc123", LinkedPr = 42 };

        var exitCode = await _command.ExecuteAsync(_context, settings);

        exitCode.Should().Be(0);
        _console.Output.Should().NotContain("at least one field flag");
        await _fleeceService.Received(1).UpdateAsync(id: "abc123", linkedPr: 42);
    }

    [Test]
    public async Task ExecuteAsync_IdWithStatusFlag_InvokesUpdate()
    {
        var updated = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Existing")
            .WithStatus(IssueStatus.Complete)
            .WithType(IssueType.Task)
            .Build();
        _fleeceService.UpdateAsync(
                id: "abc123",
                status: IssueStatus.Complete)
            .ReturnsForAnyArgs(updated);

        var settings = new EditSettings { Id = "abc123", Status = "complete" };

        var exitCode = await _command.ExecuteAsync(_context, settings);

        exitCode.Should().Be(0);
        await _fleeceService.Received(1).UpdateAsync(
            id: "abc123",
            status: IssueStatus.Complete);
    }
}
