using System.IO.Abstractions;
using Fleece.Core.Extensions;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Testably.Abstractions.Testing;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class MockFileSystemRoundTripTests
{
    [Test]
    public async Task AddFleeceCore_WithMockFileSystem_RoundTripsIssueWithoutTouchingDisk()
    {
        // Path below is fictional: real disk would reject it. MockFileSystem accepts it.
        const string basePath = "/mock-fleece-project";
        var mockFs = new MockFileSystem();
        mockFs.Directory.CreateDirectory(basePath);

        var services = new ServiceCollection();
        services.AddFleeceCore(basePath, mockFs);

        using var provider = services.BuildServiceProvider();

        var resolvedFs = provider.GetRequiredService<IFileSystem>();
        resolvedFs.Should().BeSameAs(mockFs);

        var fleece = provider.GetRequiredService<IFleeceService>();

        var created = await fleece.CreateAsync(
            title: "Mock round-trip",
            type: IssueType.Task,
            description: "Verifies writes hit the mock filesystem only.");

        created.Id.Should().NotBeNullOrWhiteSpace();

        var loaded = await fleece.GetByIdAsync(created.Id);
        loaded.Should().NotBeNull();
        loaded!.Title.Should().Be("Mock round-trip");

        var fleeceDir = mockFs.Path.Combine(basePath, ".fleece");
        mockFs.Directory.Exists(fleeceDir).Should().BeTrue();

        // Event-sourced storage writes change events to .fleece/changes/, not a hashed snapshot file.
        var changesDir = mockFs.Path.Combine(fleeceDir, "changes");
        mockFs.Directory.Exists(changesDir).Should().BeTrue();

        var changeFiles = mockFs.Directory.GetFiles(changesDir, "change_*.jsonl");
        changeFiles.Should().HaveCount(1, "exactly one active change file should exist after a single write");

        var content = await mockFs.File.ReadAllTextAsync(changeFiles[0]);
        content.Should().Contain(created.Id);
        content.Should().Contain("Mock round-trip");

        System.IO.Directory.Exists(basePath).Should().BeFalse(
            "mock path must not exist on the real disk");
    }

    [Test]
    public void AddFleeceCore_WithoutFileSystem_RegistersRealFileSystem()
    {
        var services = new ServiceCollection();
        services.AddFleeceCore(System.IO.Path.GetTempPath());

        using var provider = services.BuildServiceProvider();

        var resolvedFs = provider.GetRequiredService<IFileSystem>();
        resolvedFs.Should().BeOfType<Testably.Abstractions.RealFileSystem>();
    }
}
