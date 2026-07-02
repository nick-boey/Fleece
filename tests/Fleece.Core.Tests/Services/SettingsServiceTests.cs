using Fleece.Core.Models;
using Fleece.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class SettingsServiceTests
{
    private string _tempDir = null!;
    private string _tempGlobalDir = null!;
    private SettingsService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _tempGlobalDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempGlobalDir);
        _sut = new SettingsService(_tempDir, globalSettingsDirectory: _tempGlobalDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        if (Directory.Exists(_tempGlobalDir))
        {
            Directory.Delete(_tempGlobalDir, recursive: true);
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
            Identity = "Test User",
            SyncBranch = "fleece-sync"
        };

        await _sut.SaveLocalSettingsAsync(settings);
        var loaded = await _sut.LoadLocalSettingsAsync();

        loaded.Should().NotBeNull();
        loaded!.Identity.Should().Be("Test User");
        loaded.SyncBranch.Should().Be("fleece-sync");
    }

    [Test]
    public async Task SaveSettingsToFileAsync_CreatesDirectoryIfNeeded()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "dir", "settings.json");
        var settings = new FleeceSettings { Identity = "Test User" };

        await _sut.SaveSettingsToFileAsync(nestedPath, settings);

        File.Exists(nestedPath).Should().BeTrue();
    }

    [Test]
    public async Task GetEffectiveSettingsAsync_ReturnsDefaults_WhenNoSettingsFiles()
    {
        var result = await _sut.GetEffectiveSettingsAsync();

        result.Identity.Should().BeNull();
        result.SyncBranch.Should().BeNull();
        result.Tracker.Should().Be("github");
        result.Sources.Identity.Should().Be(SettingSource.Default);
        result.Sources.SyncBranch.Should().Be(SettingSource.Default);
        result.Sources.Tracker.Should().Be(SettingSource.Default);
    }

    [Test]
    public async Task GetEffectiveSettingsAsync_LocalOverridesDefault()
    {
        var localSettings = new FleeceSettings { Identity = "Local User" };
        await _sut.SaveLocalSettingsAsync(localSettings);

        var result = await _sut.GetEffectiveSettingsAsync();

        result.Identity.Should().Be("Local User");
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
        // Local only specifies identity, leaves syncBranch as default
        var localSettings = new FleeceSettings { Identity = "Local User" };
        await _sut.SaveLocalSettingsAsync(localSettings);

        var result = await _sut.GetEffectiveSettingsAsync();

        result.SyncBranch.Should().BeNull();  // Default
        result.Sources.SyncBranch.Should().Be(SettingSource.Default);
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
        var initialSettings = new FleeceSettings { SyncBranch = "fleece-sync" };
        await _sut.SaveLocalSettingsAsync(initialSettings);

        await _sut.SetSettingAsync("identity", "Updated User", global: false);

        var settings = await _sut.LoadLocalSettingsAsync();
        settings!.SyncBranch.Should().Be("fleece-sync"); // Preserved
        settings.Identity.Should().Be("Updated User"); // Updated
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
    public async Task GetEffectiveSettingsAsync_TrackerDefaultsToGitHub_WhenUnset()
    {
        var result = await _sut.GetEffectiveSettingsAsync();

        result.Tracker.Should().Be("github");
        result.Sources.Tracker.Should().Be(SettingSource.Default);
    }

    [Test]
    public async Task SetSettingAsync_Tracker_RoundTripsAndIsHonoured()
    {
        await _sut.SetSettingAsync("tracker", "linear", global: false);

        var settings = await _sut.LoadLocalSettingsAsync();
        settings!.Tracker.Should().Be("linear");

        var effective = await _sut.GetEffectiveSettingsAsync();
        effective.Tracker.Should().Be("linear");
        effective.Sources.Tracker.Should().Be(SettingSource.Local);
    }

    [Test]
    public async Task SetSettingAsync_Tracker_NormalizesCaseAndWhitespace()
    {
        await _sut.SetSettingAsync("tracker", "  LINEAR  ", global: false);

        var settings = await _sut.LoadLocalSettingsAsync();
        settings!.Tracker.Should().Be("linear");
    }

    [Test]
    public async Task SetSettingAsync_Tracker_EmptyStringClearsToDefault()
    {
        await _sut.SetSettingAsync("tracker", "linear", global: false);
        await _sut.SetSettingAsync("tracker", "", global: false);

        var settings = await _sut.LoadLocalSettingsAsync();
        settings!.Tracker.Should().BeNull();

        var effective = await _sut.GetEffectiveSettingsAsync();
        effective.Tracker.Should().Be("github");
        effective.Sources.Tracker.Should().Be(SettingSource.Default);
    }

    [Test]
    public void SetSettingAsync_Tracker_RejectsInvalidValue()
    {
        Func<Task> act = async () => await _sut.SetSettingAsync("tracker", "jira", global: false);

        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid tracker*jira*");
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
        var sut = new SettingsService(_tempDir);

        var path = sut.GetGlobalSettingsPath();

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
