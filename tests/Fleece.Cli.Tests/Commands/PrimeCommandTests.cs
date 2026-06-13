using Fleece.Cli;
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
using Testably.Abstractions.Testing;

namespace Fleece.Cli.Tests.Commands;

[TestFixture]
public class PrimeCommandTests
{
    private MockFileSystem _fs = null!;
    private IFleeceService _fleece = null!;
    private TestConsole _console = null!;
    private PrimeCommand _command = null!;
    private CommandContext _context = null!;
    private string _basePath = null!;

    [SetUp]
    public void SetUp()
    {
        _fs = new MockFileSystem();
        _basePath = _fs.Path.Combine(_fs.Directory.GetCurrentDirectory(), "repo");
        _fs.Directory.CreateDirectory(_basePath);

        _fleece = Substitute.For<IFleeceService>();
        _fleece.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Issue>>([]));

        _console = new TestConsole();
        _command = new PrimeCommand(_fleece, _fs, new BasePathProvider(_basePath), _console);
        _context = new CommandContext([], Substitute.For<IRemainingArguments>(), "prime", null);
    }

    [TearDown]
    public void TearDown()
    {
        _console.Dispose();
    }

    private void CreateFleeceDirectory()
    {
        _fs.Directory.CreateDirectory(_fs.Path.Combine(_basePath, ".fleece"));
    }

    [Test]
    public async Task Execute_NoFleeceDirectory_NoTopic_ExitsSilentlyWithCodeZero()
    {
        var result = await _command.ExecuteAsync(_context, new PrimeSettings());

        result.Should().Be(0);
        _console.Output.Should().BeEmpty();
    }

    [Test]
    public async Task Execute_NoFleeceDirectory_WithTopic_ExitsSilentlyWithCodeZero()
    {
        var result = await _command.ExecuteAsync(_context, new PrimeSettings { Topic = "commands" });

        result.Should().Be(0);
        _console.Output.Should().BeEmpty();
    }

    [Test]
    public async Task Execute_FleecePresent_NoTopic_EmitsEphemeralMemoryOverview()
    {
        CreateFleeceDirectory();

        var result = await _command.ExecuteAsync(_context, new PrimeSettings());

        result.Should().Be(0);
        var output = _console.Output;
        output.Should().Contain("Ephemeral Agent Working Memory");
        output.Should().Contain("active issue(s)");
    }

    [Test]
    public async Task Execute_FleecePresent_NoTopic_ReportsActiveIssueCount()
    {
        CreateFleeceDirectory();
        _fleece.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Issue>>(
            [
                new IssueBuilder().WithId("aaaaaa").WithTitle("Open work").WithStatus(IssueStatus.Open).WithType(IssueType.Task).Build(),
                new IssueBuilder().WithId("bbbbbb").WithTitle("In progress").WithStatus(IssueStatus.Progress).WithType(IssueType.Task).Build(),
                new IssueBuilder().WithId("cccccc").WithTitle("Done").WithStatus(IssueStatus.Complete).WithType(IssueType.Task).Build()
            ]));

        var result = await _command.ExecuteAsync(_context, new PrimeSettings());

        result.Should().Be(0);
        _console.Output.Should().Contain("2 active issue(s)");
    }

    [Test]
    public async Task Execute_FleecePresent_KnownTopic_EmitsTopicContent()
    {
        CreateFleeceDirectory();

        var result = await _command.ExecuteAsync(_context, new PrimeSettings { Topic = "v4-migration" });

        result.Should().Be(0);
        _console.Output.Should().NotBeEmpty();
    }

    [Test]
    public async Task Execute_FleecePresent_UnknownTopic_ListsAvailableTopics()
    {
        CreateFleeceDirectory();

        var result = await _command.ExecuteAsync(_context, new PrimeSettings { Topic = "not-a-real-topic" });

        result.Should().NotBe(0);
        var output = _console.Output;
        output.Should().Contain("Unknown topic: not-a-real-topic");
        output.Should().Contain("Available topics:");
    }
}
