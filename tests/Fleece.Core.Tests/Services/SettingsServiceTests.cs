using Fleece.Core.Models;
using Fleece.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class SettingsServiceTests
{
    private string _tempDir = null!;
    private SettingsService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _sut = new SettingsService(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Test]
    public async Task LoadLocalSettingsAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        var result = await _sut.LoadLocalSettingsAsync();

        result.Should().BeNull();
    }

    [Test]
    public async Task LoadSettingsFromFileAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        var result = await _sut.LoadSettingsFromFileAsync("/nonexistent/path/settings.json");

        result.Should().BeNull();
    }

    [Test]
    public async Task SaveAndLoadLocalSettings_RoundTrips()
    {
        var settings = new FleeceSettings
        {
            AutoMerge = true,
            Identity = "Test User",
            SyncBranch = "fleece-sync"
        };

        await _sut.SaveLocalSettingsAsync(settings);
        var loaded = await _sut.LoadLocalSettingsAsync();

        loaded.Should().NotBeNull();
        loaded!.AutoMerge.Should().BeTrue();
        loaded.Identity.Should().Be("Test User");
        loaded.SyncBranch.Should().Be("fleece-sync");
    }

    [Test]
    public async Task SaveSettingsToFileAsync_CreatesDirectoryIfNeeded()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "dir", "settings.json");
        var settings = new FleeceSettings { AutoMerge = true };

        await _sut.SaveSettingsToFileAsync(nestedPath, settings);

        File.Exists(nestedPath).Should().BeTrue();
    }

    [Test]
    public async Task GetEffectiveSettingsAsync_ReturnsDefaults_WhenNoSettingsFiles()
    {
        var result = await _sut.GetEffectiveSettingsAsync();

        result.AutoMerge.Should().BeFalse();
        result.Identity.Should().BeNull();
        result.SyncBranch.Should().BeNull();
        result.Sources.AutoMerge.Should().Be(SettingSource.Default);
        result.Sources.Identity.Should().Be(SettingSource.Default);
        result.Sources.SyncBranch.Should().Be(SettingSource.Default);
    }

    [Test]
    public async Task GetEffectiveSettingsAsync_LocalOverridesDefault()
    {
        var localSettings = new FleeceSettings { AutoMerge = true, Identity = "Local User" };
        await _sut.SaveLocalSettingsAsync(localSettings);

        var result = await _sut.GetEffectiveSettingsAsync();

        result.AutoMerge.Should().BeTrue();
        result.Identity.Should().Be("Local User");
        result.Sources.AutoMerge.Should().Be(SettingSource.Local);
        result.Sources.Identity.Should().Be(SettingSource.Local);
    }

    [Test]
    public async Task GetEffectiveSettingsAsync_CliOverridesLocal()
    {
        var localSettings = new FleeceSettings { Identity = "Local User" };
        await _sut.SaveLocalSettingsAsync(localSettings);

        var cliOverrides = new FleeceSettings { Identity = "CLI User" };
        var result = await _sut.GetEffectiveSettingsAsync(cliOverrides);

        result.Identity.Should().Be("CLI User");
        result.Sources.Identity.Should().Be(SettingSource.CommandLine);
    }

    [Test]
    public async Task GetEffectiveSettingsAsync_PartialLocalSettings_OnlyOverridesSpecified()
    {
        // Local only specifies identity, leaves autoMerge as default
        var localSettings = new FleeceSettings { Identity = "Local User" };
        await _sut.SaveLocalSettingsAsync(localSettings);

        var result = await _sut.GetEffectiveSettingsAsync();

        result.AutoMerge.Should().BeFalse();
        result.Sources.AutoMerge.Should().Be(SettingSource.Default);
        result.Identity.Should().Be("Local User");
        result.Sources.Identity.Should().Be(SettingSource.Local);
    }

    [Test]
    public async Task SetSettingAsync_CreatesFileIfNotExists()
    {
        await _sut.SetSettingAsync("identity", "New User", global: false);

        var settings = await _sut.LoadLocalSettingsAsync();
        settings.Should().NotBeNull();
        settings!.Identity.Should().Be("New User");
    }

    [Test]
    public async Task SetSettingAsync_UpdatesExistingFile()
    {
        var initialSettings = new FleeceSettings { AutoMerge = true };
        await _sut.SaveLocalSettingsAsync(initialSettings);

        await _sut.SetSettingAsync("identity", "Updated User", global: false);

        var settings = await _sut.LoadLocalSettingsAsync();
        settings!.AutoMerge.Should().BeTrue(); // Preserved
        settings.Identity.Should().Be("Updated User"); // Updated
    }

    [Test]
    public async Task SetSettingAsync_AutoMerge_ParsesBoolean()
    {
        await _sut.SetSettingAsync("autoMerge", "true", global: false);
        var settings = await _sut.LoadLocalSettingsAsync();
        settings!.AutoMerge.Should().BeTrue();

        await _sut.SetSettingAsync("autoMerge", "false", global: false);
        settings = await _sut.LoadLocalSettingsAsync();
        settings!.AutoMerge.Should().BeFalse();
    }

    [Test]
    public async Task SetSettingAsync_AutoMerge_AcceptsAlternativeBooleanFormats()
    {
        await _sut.SetSettingAsync("autoMerge", "yes", global: false);
        var settings = await _sut.LoadLocalSettingsAsync();
        settings!.AutoMerge.Should().BeTrue();

        await _sut.SetSettingAsync("autoMerge", "no", global: false);
        settings = await _sut.LoadLocalSettingsAsync();
        settings!.AutoMerge.Should().BeFalse();

        await _sut.SetSettingAsync("autoMerge", "1", global: false);
        settings = await _sut.LoadLocalSettingsAsync();
        settings!.AutoMerge.Should().BeTrue();

        await _sut.SetSettingAsync("autoMerge", "0", global: false);
        settings = await _sut.LoadLocalSettingsAsync();
        settings!.AutoMerge.Should().BeFalse();
    }

    [Test]
    public async Task SetSettingAsync_ClearsValueWithEmptyString()
    {
        var initialSettings = new FleeceSettings { Identity = "Some User" };
        await _sut.SaveLocalSettingsAsync(initialSettings);

        await _sut.SetSettingAsync("identity", "", global: false);

        var settings = await _sut.LoadLocalSettingsAsync();
        settings!.Identity.Should().BeNull();
    }

    [Test]
    public void SetSettingAsync_ThrowsForUnknownKey()
    {
        Func<Task> act = async () => await _sut.SetSettingAsync("unknownKey", "value", global: false);

        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unknown setting*unknownKey*");
    }

    [Test]
    public void SetSettingAsync_ThrowsForInvalidBoolean()
    {
        Func<Task> act = async () => await _sut.SetSettingAsync("autoMerge", "invalid", global: false);

        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid boolean value*");
    }

    [Test]
    public void GetLocalSettingsPath_ReturnsExpectedPath()
    {
        var path = _sut.GetLocalSettingsPath();

        path.Should().Be(Path.Combine(_tempDir, ".fleece", "settings.json"));
    }

    [Test]
    public void GetGlobalSettingsPath_ReturnsExpectedPath()
    {
        var path = _sut.GetGlobalSettingsPath();

        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".fleece",
            "settings.json");
        path.Should().Be(expectedPath);
    }

    [Test]
    public async Task LoadSettingsFromFileAsync_ReturnsNull_ForMalformedJson()
    {
        var settingsPath = Path.Combine(_tempDir, ".fleece", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        await File.WriteAllTextAsync(settingsPath, "{ invalid json }");

        var result = await _sut.LoadLocalSettingsAsync();

        result.Should().BeNull();
    }
}
