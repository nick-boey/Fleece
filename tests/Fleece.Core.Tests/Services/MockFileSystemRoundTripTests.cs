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

        // v4 per-issue logs: each issue is a single append-only log at .fleece/issues/<id>.jsonl.
        // There is no projected snapshot file and no .fleece/changes/ directory.
        var issuesDir = mockFs.Path.Combine(fleeceDir, "issues");
        mockFs.Directory.Exists(issuesDir).Should().BeTrue();
        mockFs.Directory.Exists(mockFs.Path.Combine(fleeceDir, "changes")).Should().BeFalse();
        mockFs.File.Exists(mockFs.Path.Combine(fleeceDir, "issues.jsonl")).Should().BeFalse();

        var logFiles = mockFs.Directory.GetFiles(issuesDir, "*.jsonl");
        logFiles.Should().HaveCount(1, "exactly one per-issue log should exist after a single create");

        var logPath = mockFs.Path.Combine(issuesDir, $"{created.Id}.jsonl");
        mockFs.File.Exists(logPath).Should().BeTrue("the log file is named after the issue id");

        var content = await mockFs.File.ReadAllTextAsync(logFiles[0]);
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
