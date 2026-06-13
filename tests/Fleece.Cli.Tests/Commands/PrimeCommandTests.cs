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
    public async Task Execute_NoFleeceDirectory_ExitsSilentlyWithCodeZero()
    {
        var result = await _command.ExecuteAsync(_context, new PrimeSettings());

        result.Should().Be(0);
        _console.Output.Should().BeEmpty();
    }

    [Test]
    public async Task Execute_FleecePresent_ZeroIssues_EmitsNothing()
    {
        CreateFleeceDirectory();

        var result = await _command.ExecuteAsync(_context, new PrimeSettings());

        result.Should().Be(0);
        _console.Output.Should().BeEmpty();
    }

    [Test]
    public async Task Execute_FleecePresent_AllIssuesInactive_EmitsNothing()
    {
        CreateFleeceDirectory();
        _fleece.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Issue>>(
            [
                new IssueBuilder().WithId("aaaaaa").WithTitle("Done").WithStatus(IssueStatus.Complete).WithType(IssueType.Task).Build(),
                new IssueBuilder().WithId("bbbbbb").WithTitle("Gone").WithStatus(IssueStatus.Closed).WithType(IssueType.Task).Build(),
                new IssueBuilder().WithId("cccccc").WithTitle("Up").WithStatus(IssueStatus.Promoted).WithType(IssueType.Task).Build()
            ]));

        var result = await _command.ExecuteAsync(_context, new PrimeSettings());

        result.Should().Be(0);
        _console.Output.Should().BeEmpty();
    }

    [Test]
    public async Task Execute_FleecePresent_ActiveIssues_EmitsCountAndSkillPointer()
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
        var output = _console.Output;
        output.Should().Contain("2 active issue(s)");
        output.Should().Contain(".claude/skills/fleece");
        // The slim hook no longer carries the static reference — that lives in the skill.
        output.Should().NotContain("Issue Types");
        output.Should().NotContain("Detailed Topics");
        output.Should().NotContain("Storage model");
    }
}
