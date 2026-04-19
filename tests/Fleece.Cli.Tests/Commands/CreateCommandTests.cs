using Fleece.Cli.Commands;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Fleece.Cli.Tests.Commands;

[TestFixture]
public class CreateCommandTests
{
    private IFleeceService _fleeceService = null!;
    private ISettingsService _settingsService = null!;
    private IGitConfigService _gitConfigService = null!;
    private IGitService _gitService = null!;
    private CreateCommand _command = null!;
    private CommandContext _context = null!;
    private TestConsole _console = null!;

    [SetUp]
    public void SetUp()
    {
        _fleeceService = Substitute.For<IFleeceService>();
        _fleeceService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((false, string.Empty));

        _settingsService = Substitute.For<ISettingsService>();
        _gitConfigService = Substitute.For<IGitConfigService>();
        _gitService = Substitute.For<IGitService>();
        _console = new TestConsole();

        _command = new CreateCommand(_fleeceService, _settingsService, _gitConfigService, _gitService, _console);
        _context = new CommandContext([], Substitute.For<IRemainingArguments>(), "create", null);
    }

    [TearDown]
    public void TearDown()
    {
        _console.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_NoArgs_ReturnsErrorForMissingTitle()
    {
        var settings = new CreateSettings();

        var exitCode = await _command.ExecuteAsync(_context, settings);

        exitCode.Should().Be(1);
        _console.Output.Should().Contain("--title is required");
        await _fleeceService.DidNotReceiveWithAnyArgs().CreateAsync(
            title: Arg.Any<string>(),
            type: Arg.Any<IssueType>());
    }

    [Test]
    public async Task ExecuteAsync_TitleWithoutType_ReturnsErrorForMissingType()
    {
        var settings = new CreateSettings { Title = "My issue" };

        var exitCode = await _command.ExecuteAsync(_context, settings);

        exitCode.Should().Be(1);
        _console.Output.Should().Contain("--type is required");
        await _fleeceService.DidNotReceiveWithAnyArgs().CreateAsync(
            title: Arg.Any<string>(),
            type: Arg.Any<IssueType>());
    }
}
