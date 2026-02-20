using Fleece.Cli.Interceptors;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Tests.Interceptors;

[TestFixture]
public class AutoMergeInterceptorTests
{
    private ISettingsService _settingsService = null!;
    private IStorageService _storageService = null!;
    private IMergeService _mergeService = null!;
    private IServiceProvider _serviceProvider = null!;
    private AutoMergeInterceptor _interceptor = null!;
    private StringWriter _consoleOutput = null!;
    private TextWriter _originalConsole = null!;
    private IAnsiConsole _originalAnsiConsole = null!;

    [SetUp]
    public void SetUp()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _storageService = Substitute.For<IStorageService>();
        _mergeService = Substitute.For<IMergeService>();

        var services = new ServiceCollection();
        services.AddSingleton(_settingsService);
        services.AddSingleton(_storageService);
        services.AddSingleton(_mergeService);
        _serviceProvider = services.BuildServiceProvider();

        _interceptor = new AutoMergeInterceptor(() => _serviceProvider);

        // Capture console output
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

    private CommandContext CreateContext(string commandName)
    {
        return new CommandContext([], Substitute.For<IRemainingArguments>(), commandName, null);
    }

    [Test]
    public void Intercept_WhenAutoMergeDisabled_DoesNotMerge()
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

        var context = CreateContext("list");
        var settings = Substitute.For<CommandSettings>();

        _interceptor.Intercept(context, settings);

        _mergeService.DidNotReceive().FindAndResolveDuplicatesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Intercept_WhenNoMultipleFiles_DoesNotMerge()
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
        _storageService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((false, string.Empty));

        var context = CreateContext("list");
        var settings = Substitute.For<CommandSettings>();

        _interceptor.Intercept(context, settings);

        _mergeService.DidNotReceive().FindAndResolveDuplicatesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Intercept_WhenAutoMergeEnabledAndMultipleFiles_PerformsMerge()
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
        _storageService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((true, "Multiple files found"));
        _mergeService.FindAndResolveDuplicatesAsync(false, Arg.Any<CancellationToken>())
            .Returns(5);

        var context = CreateContext("list");
        var settings = Substitute.For<CommandSettings>();

        _interceptor.Intercept(context, settings);

        _mergeService.Received(1).FindAndResolveDuplicatesAsync(false, Arg.Any<CancellationToken>());
    }

    [Test]
    public void Intercept_WhenMergePerformed_ShowsOneLineOutput()
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
        _storageService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((true, "Multiple files found"));
        _mergeService.FindAndResolveDuplicatesAsync(false, Arg.Any<CancellationToken>())
            .Returns(10);

        var context = CreateContext("list");
        var settings = Substitute.For<CommandSettings>();

        _interceptor.Intercept(context, settings);

        var output = _consoleOutput.ToString();
        output.Should().Contain("Auto-merged 10 issue(s)");
    }

    [Test]
    public void Intercept_WhenMergeReturnsZero_DoesNotShowOutput()
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
        _storageService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((true, "Multiple files found"));
        _mergeService.FindAndResolveDuplicatesAsync(false, Arg.Any<CancellationToken>())
            .Returns(0);

        var context = CreateContext("list");
        var settings = Substitute.For<CommandSettings>();

        _interceptor.Intercept(context, settings);

        var output = _consoleOutput.ToString();
        output.Should().BeEmpty();
    }

    [TestCase("merge")]
    [TestCase("diff")]
    [TestCase("install")]
    [TestCase("prime")]
    [TestCase("config")]
    [TestCase("migrate")]
    [TestCase("commit")]
    public void Intercept_ForSkippedCommands_DoesNotMerge(string commandName)
    {
        var context = CreateContext(commandName);
        var settings = Substitute.For<CommandSettings>();

        _interceptor.Intercept(context, settings);

        _settingsService.DidNotReceive().GetEffectiveSettingsAsync(Arg.Any<FleeceSettings?>(), Arg.Any<CancellationToken>());
        _mergeService.DidNotReceive().FindAndResolveDuplicatesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Intercept_ForEmptyCommandName_DoesNotMerge()
    {
        var context = CreateContext(string.Empty);
        var settings = Substitute.For<CommandSettings>();

        _interceptor.Intercept(context, settings);

        _settingsService.DidNotReceive().GetEffectiveSettingsAsync(Arg.Any<FleeceSettings?>(), Arg.Any<CancellationToken>());
    }

    [TestCase("list")]
    [TestCase("tree")]
    [TestCase("create")]
    [TestCase("edit")]
    [TestCase("delete")]
    [TestCase("show")]
    [TestCase("search")]
    [TestCase("clean")]
    [TestCase("validate")]
    [TestCase("next")]
    [TestCase("question")]
    [TestCase("dependency")]
    public void Intercept_ForNonSkippedCommands_ChecksSettings(string commandName)
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

        var context = CreateContext(commandName);
        var settings = Substitute.For<CommandSettings>();

        _interceptor.Intercept(context, settings);

        _settingsService.Received(1).GetEffectiveSettingsAsync(Arg.Any<FleeceSettings?>(), Arg.Any<CancellationToken>());
    }
}
