using Fleece.Cli.Commands;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Fleece.Cli.Tests.Commands;

[TestFixture]
public class ConfigCommandTests
{
    private ISettingsService _settingsService = null!;
    private ConfigCommand _command = null!;
    private CommandContext _context = null!;
    private TestConsole _console = null!;
    private StringWriter _consoleOutput = null!;
    private TextWriter _originalConsole = null!;

    [SetUp]
    public void SetUp()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _console = new TestConsole();
        _command = new ConfigCommand(_settingsService, _console);
        _context = new CommandContext([], Substitute.For<IRemainingArguments>(), "config", null);

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
    public async Task ExecuteAsync_Path_ShowsLocalPath()
    {
        _settingsService.GetLocalSettingsPath().Returns("/test/path/.fleece/settings.json");

        var settings = new ConfigSettings { Path = true };
        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);
        _consoleOutput.ToString().Trim().Should().Be("/test/path/.fleece/settings.json");
    }

    [Test]
    public async Task ExecuteAsync_PathGlobal_ShowsGlobalPath()
    {
        _settingsService.GetGlobalSettingsPath().Returns("/home/user/.fleece/settings.json");

        var settings = new ConfigSettings { Path = true, Global = true };
        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);
        _consoleOutput.ToString().Trim().Should().Be("/home/user/.fleece/settings.json");
    }

    [Test]
    public async Task ExecuteAsync_List_ShowsAllSettings()
    {
        var effectiveSettings = new EffectiveSettings
        {
            AutoMerge = true,
            Identity = "Test User",
            SyncBranch = null,
            Sources = new SettingsSources
            {
                AutoMerge = SettingSource.Local,
                Identity = SettingSource.Global,
                SyncBranch = SettingSource.Default
            }
        };
        _settingsService.GetEffectiveSettingsAsync(Arg.Any<FleeceSettings?>(), Arg.Any<CancellationToken>())
            .Returns(effectiveSettings);

        var settings = new ConfigSettings { List = true };
        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);
        var output = CombinedOutput();
        output.Should().Contain("autoMerge");
        output.Should().Contain("true");
        output.Should().Contain("identity");
        output.Should().Contain("Test User");
    }

    [Test]
    public async Task ExecuteAsync_ListJson_OutputsJson()
    {
        var effectiveSettings = new EffectiveSettings
        {
            AutoMerge = false,
            Identity = "Test User",
            SyncBranch = "sync-branch",
            Sources = new SettingsSources
            {
                AutoMerge = SettingSource.Default,
                Identity = SettingSource.Local,
                SyncBranch = SettingSource.Global
            }
        };
        _settingsService.GetEffectiveSettingsAsync(Arg.Any<FleeceSettings?>(), Arg.Any<CancellationToken>())
            .Returns(effectiveSettings);

        var settings = new ConfigSettings { List = true, Json = true };
        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);
        var output = CombinedOutput();
        output.Should().Contain("\"autoMerge\"");
        output.Should().Contain("\"identity\"");
        output.Should().Contain("\"syncBranch\"");
        output.Should().Contain("\"source\"");
    }

    [Test]
    public async Task ExecuteAsync_GetAutoMerge_ReturnsValue()
    {
        var effectiveSettings = new EffectiveSettings
        {
            AutoMerge = true,
            Identity = null,
            SyncBranch = null,
            Sources = new SettingsSources
            {
                AutoMerge = SettingSource.Local,
                Identity = SettingSource.Default,
                SyncBranch = SettingSource.Default
            }
        };
        _settingsService.GetEffectiveSettingsAsync(Arg.Any<FleeceSettings?>(), Arg.Any<CancellationToken>())
            .Returns(effectiveSettings);

        var settings = new ConfigSettings { Get = "autoMerge" };
        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);
        _consoleOutput.ToString().Trim().Should().Be("true");
    }

    [Test]
    public async Task ExecuteAsync_GetIdentity_ReturnsValue()
    {
        var effectiveSettings = new EffectiveSettings
        {
            AutoMerge = false,
            Identity = "John Doe",
            SyncBranch = null,
            Sources = new SettingsSources
            {
                AutoMerge = SettingSource.Default,
                Identity = SettingSource.Global,
                SyncBranch = SettingSource.Default
            }
        };
        _settingsService.GetEffectiveSettingsAsync(Arg.Any<FleeceSettings?>(), Arg.Any<CancellationToken>())
            .Returns(effectiveSettings);

        var settings = new ConfigSettings { Get = "identity" };
        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);
        _consoleOutput.ToString().Trim().Should().Be("John Doe");
    }

    [Test]
    public async Task ExecuteAsync_GetUnknownKey_ReturnsError()
    {
        var effectiveSettings = new EffectiveSettings
        {
            AutoMerge = false,
            Identity = null,
            SyncBranch = null,
            Sources = new SettingsSources
            {
                AutoMerge = SettingSource.Default,
                Identity = SettingSource.Default,
                SyncBranch = SettingSource.Default
            }
        };
        _settingsService.GetEffectiveSettingsAsync(Arg.Any<FleeceSettings?>(), Arg.Any<CancellationToken>())
            .Returns(effectiveSettings);

        var settings = new ConfigSettings { Get = "unknownKey" };
        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(1);
        CombinedOutput().Should().Contain("Unknown setting");
    }

    [Test]
    public async Task ExecuteAsync_SetLocal_CallsSetSettingAsync()
    {
        var settings = new ConfigSettings { Set = "identity=New User" };
        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);
        await _settingsService.Received(1).SetSettingAsync("identity", "New User", false, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_SetGlobal_CallsSetSettingAsyncWithGlobalFlag()
    {
        var settings = new ConfigSettings { Set = "autoMerge=true", Global = true };
        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);
        await _settingsService.Received(1).SetSettingAsync("autoMerge", "true", true, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_SetInvalidFormat_ReturnsError()
    {
        var settings = new ConfigSettings { Set = "invalidformat" };
        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(1);
        CombinedOutput().Should().Contain("Invalid format");
    }

    [Test]
    public async Task ExecuteAsync_SetThrowsArgumentException_ReturnsError()
    {
        _settingsService
            .When(x => x.SetSettingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new ArgumentException("Unknown setting: badKey"));

        var settings = new ConfigSettings { Set = "badKey=value" };
        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(1);
        CombinedOutput().Should().Contain("Unknown setting");
    }

    [Test]
    public async Task ExecuteAsync_NoOptions_ShowsUsage()
    {
        var settings = new ConfigSettings();
        var result = await _command.ExecuteAsync(_context, settings);

        result.Should().Be(0);
        var output = CombinedOutput();
        output.Should().Contain("Usage:");
        output.Should().Contain("--list");
        output.Should().Contain("--get");
        output.Should().Contain("--set");
    }
}
