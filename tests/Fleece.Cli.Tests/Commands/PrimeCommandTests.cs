using Fleece.Cli.Commands;
using Fleece.Cli.Settings;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Spectre.Console.Cli;

namespace Fleece.Cli.Tests.Commands;

[TestFixture]
[NonParallelizable]
public class PrimeCommandTests
{
    private PrimeCommand _command = null!;
    private CommandContext _context = null!;
    private StringWriter _consoleOutput = null!;
    private TextWriter _originalConsole = null!;
    private string _originalCwd = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _command = new PrimeCommand();
        _context = new CommandContext([], Substitute.For<IRemainingArguments>(), "prime", null);

        _originalConsole = Console.Out;
        _consoleOutput = new StringWriter();
        Console.SetOut(_consoleOutput);

        _originalCwd = Directory.GetCurrentDirectory();
        _tempDir = Path.Combine(Path.GetTempPath(), $"fleece-prime-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        Directory.SetCurrentDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetOut(_originalConsole);
        _consoleOutput.Dispose();

        Directory.SetCurrentDirectory(_originalCwd);
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Test]
    public void Execute_NoFleeceDirectory_NoTopic_ExitsSilentlyWithCodeZero()
    {
        var result = _command.Execute(_context, new PrimeSettings());

        result.Should().Be(0);
        _consoleOutput.ToString().Should().BeEmpty();
    }

    [Test]
    public void Execute_NoFleeceDirectory_WithTopic_ExitsSilentlyWithCodeZero()
    {
        var result = _command.Execute(_context, new PrimeSettings { Topic = "openspec" });

        result.Should().Be(0);
        _consoleOutput.ToString().Should().BeEmpty();
    }

    [Test]
    public void Execute_FleecePresent_NoOpenSpec_NoTopic_OmitsOpenSpecSection()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".fleece"));

        var result = _command.Execute(_context, new PrimeSettings());

        result.Should().Be(0);
        var output = _consoleOutput.ToString();
        output.Should().Contain("# Fleece Issue Tracking");
        output.Should().Contain("## Detailed Help Topics");
        output.Should().NotContain("# OpenSpec Integration");
    }

    [Test]
    public void Execute_FleecePresent_OpenSpecPresent_NoTopic_IncludesOpenSpecSection()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".fleece"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "openspec"));

        var result = _command.Execute(_context, new PrimeSettings());

        result.Should().Be(0);
        var output = _consoleOutput.ToString();

        output.Should().Contain("# Fleece Issue Tracking");
        output.Should().Contain("# OpenSpec Integration");
        output.Should().Contain("openspec=");
        output.Should().Contain("+<id>");
        output.Should().Contain("one issue per change");
        output.Should().Contain("Never create issues per task");
    }

    [Test]
    public void Execute_FleecePresent_OpenSpecTopic_EmitsOpenSpecContent_WhenOpenSpecAbsent()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".fleece"));

        var result = _command.Execute(_context, new PrimeSettings { Topic = "openspec" });

        result.Should().Be(0);
        var output = _consoleOutput.ToString();
        output.Should().Contain("# OpenSpec Integration");
        output.Should().Contain("openspec={change-name}");
    }

    [Test]
    public void Execute_FleecePresent_OpenSpecTopic_EmitsOpenSpecContent_WhenOpenSpecPresent()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".fleece"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "openspec"));

        var result = _command.Execute(_context, new PrimeSettings { Topic = "openspec" });

        result.Should().Be(0);
        var output = _consoleOutput.ToString();
        output.Should().Contain("# OpenSpec Integration");
    }

    [Test]
    public void Execute_FleecePresent_UnknownTopic_ListsOpenSpecAmongAvailableTopics()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".fleece"));

        var result = _command.Execute(_context, new PrimeSettings { Topic = "not-a-real-topic" });

        result.Should().NotBe(0);
        var output = _consoleOutput.ToString();
        output.Should().Contain("Unknown topic: not-a-real-topic");
        output.Should().Contain("Available topics:");
        output.Should().Contain("openspec");
    }
}
