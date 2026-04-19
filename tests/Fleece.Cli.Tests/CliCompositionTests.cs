using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Fleece.Cli.Tests;

[TestFixture]
public class CliCompositionTests
{
    private string _tempDir = null!;
    private ServiceProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fleece-ci-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _provider = CliComposition.BuildServices(_tempDir).BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Test]
    public void ISettingsService_resolves()
        => _provider.GetService<ISettingsService>().Should().NotBeNull();

    [Test]
    public void IFleeceService_resolves()
        => _provider.GetService<IFleeceService>().Should().NotBeNull();

    [Test]
    public void IFleeceInMemoryService_resolves()
        => _provider.GetService<IFleeceInMemoryService>().Should().NotBeNull();

    [TestCaseSource(nameof(AllCommandTypes))]
    public void Every_command_can_be_constructed_from_DI(Type commandType)
    {
        var instance = ActivatorUtilities.CreateInstance(_provider, commandType);
        instance.Should().NotBeNull();
    }

    public static IEnumerable<Type> AllCommandTypes()
        => CliComposition.Commands.Select(c => c.CommandType);
}
