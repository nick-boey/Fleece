using Fleece.Cli.Interceptors;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Fleece.Cli.Tests.Interceptors;

[TestFixture]
public class AutoMergeInterceptorTests
{
    private ISettingsService _settingsService = null!;
    private IFleeceService _fleeceService = null!;
    private IServiceProvider _serviceProvider = null!;
    private AutoMergeInterceptor _interceptor = null!;
    private TestConsole _console = null!;

    [SetUp]
    public void SetUp()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _fleeceService = Substitute.For<IFleeceService>();
        _console = new TestConsole();

        var services = new ServiceCollection();
        services.AddSingleton(_settingsService);
        services.AddSingleton(_fleeceService);
        services.AddSingleton<IAnsiConsole>(_console);
        _serviceProvider = services.BuildServiceProvider();

        _interceptor = new AutoMergeInterceptor(() => _serviceProvider);
    }

    [TearDown]
    public void TearDown()
    {
        _console.Dispose();
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

        _fleeceService.DidNotReceive().MergeAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
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
        _fleeceService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((false, string.Empty));

        var context = CreateContext("list");
        var settings = Substitute.For<CommandSettings>();

        _interceptor.Intercept(context, settings);

        _fleeceService.DidNotReceive().MergeAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
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
        _fleeceService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((true, "Multiple files found"));
        _fleeceService.MergeAsync(false, Arg.Any<CancellationToken>())
            .Returns(5);

        var context = CreateContext("list");
        var settings = Substitute.For<CommandSettings>();

        _interceptor.Intercept(context, settings);

        _fleeceService.Received(1).MergeAsync(false, Arg.Any<CancellationToken>());
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
        _fleeceService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((true, "Multiple files found"));
        _fleeceService.MergeAsync(false, Arg.Any<CancellationToken>())
            .Returns(10);

        var context = CreateContext("list");
        var settings = Substitute.For<CommandSettings>();

        _interceptor.Intercept(context, settings);

        _console.Output.Should().Contain("Auto-merged 10 issue(s)");
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
        _fleeceService.HasMultipleUnmergedFilesAsync(Arg.Any<CancellationToken>())
            .Returns((true, "Multiple files found"));
        _fleeceService.MergeAsync(false, Arg.Any<CancellationToken>())
            .Returns(0);

        var context = CreateContext("list");
        var settings = Substitute.For<CommandSettings>();

        _interceptor.Intercept(context, settings);

        _console.Output.Should().BeEmpty();
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
        _fleeceService.DidNotReceive().MergeAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
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
