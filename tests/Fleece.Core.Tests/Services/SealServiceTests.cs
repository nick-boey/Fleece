using System.IO.Abstractions;
using Fleece.Core.Extensions;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Testably.Abstractions.Testing;

namespace Fleece.Core.Tests.Services;

/// <summary>
/// Tests the <c>fleece seal</c> branch-lifecycle operation: gating on inactive status,
/// content-addressed archive output, live-log clearing, and the empty-set no-op.
/// </summary>
[TestFixture]
public sealed class SealServiceTests
{
    private const string BasePath = "/project";

    private MockFileSystem _fs = null!;
    private ServiceProvider _provider = null!;
    private IFleeceService _fleece = null!;
    private ISealService _seal = null!;

    private string IssuesDir => _fs.Path.Combine(BasePath, ".fleece", "issues");
    private string ArchiveDir => _fs.Path.Combine(BasePath, ".fleece", "archive");

    [SetUp]
    public void SetUp()
    {
        _fs = new MockFileSystem();
        _fs.Directory.CreateDirectory(BasePath);

        var services = new ServiceCollection();
        services.AddFleeceCore(BasePath, _fs);
        _provider = services.BuildServiceProvider();

        _fleece = _provider.GetRequiredService<IFleeceService>();
        _seal = _provider.GetRequiredService<ISealService>();
    }

    [TearDown]
    public void TearDown() => _provider.Dispose();

    [Test]
    public async Task Seal_empty_issue_set_is_a_noop_success()
    {
        var result = await _seal.SealAsync();

        result.Sealed.Should().BeTrue();
        result.ArchivePath.Should().BeNull();
        result.RemovedCount.Should().Be(0);
        _fs.Directory.Exists(ArchiveDir).Should().BeFalse();
    }

    [Test]
    public async Task Seal_refuses_while_an_active_issue_remains()
    {
        var active = await _fleece.CreateAsync("Still open", IssueType.Task, status: IssueStatus.Open);
        var done = await _fleece.CreateAsync("Done", IssueType.Task, status: IssueStatus.Complete);

        var result = await _seal.SealAsync();

        result.Sealed.Should().BeFalse();
        result.ActiveIssues.Select(i => i.Id).Should().Contain(active.Id);
        result.ActiveIssues.Select(i => i.Id).Should().NotContain(done.Id);

        // No changes: live logs remain, no archive written.
        _fs.Directory.GetFiles(IssuesDir, "*.jsonl").Should().HaveCount(2);
        _fs.Directory.Exists(ArchiveDir).Should().BeFalse();
    }

    [Test]
    public async Task Seal_treats_progress_and_review_as_active()
    {
        await _fleece.CreateAsync("In progress", IssueType.Task, status: IssueStatus.Progress);
        await _fleece.CreateAsync("In review", IssueType.Task, status: IssueStatus.Review);

        var result = await _seal.SealAsync();

        result.Sealed.Should().BeFalse();
        result.ActiveIssues.Should().HaveCount(2);
    }

    [Test]
    public async Task Seal_archives_and_clears_when_all_issues_are_inactive()
    {
        await _fleece.CreateAsync("Complete one", IssueType.Task, status: IssueStatus.Complete);
        await _fleece.CreateAsync("Closed one", IssueType.Task, status: IssueStatus.Closed);
        await _fleece.CreateAsync("Promoted one", IssueType.Task, status: IssueStatus.Promoted);

        var result = await _seal.SealAsync();

        result.Sealed.Should().BeTrue();
        result.RemovedCount.Should().Be(3);
        result.ArchivePath.Should().NotBeNull();

        // Archive written under .fleece/archive/, live issues directory emptied.
        _fs.File.Exists(result.ArchivePath!).Should().BeTrue();
        _fs.Path.GetFileName(result.ArchivePath!).Should().MatchRegex(@"^issues_[0-9a-f]{12}\.jsonl$");
        _fs.Directory.GetFiles(IssuesDir, "*.jsonl").Should().BeEmpty();
    }

    [Test]
    public async Task Seal_archive_name_is_stable_across_write_order()
    {
        var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var alpha = new Issue
        {
            Id = "id-alpha", Title = "Alpha", Type = IssueType.Task,
            Status = IssueStatus.Complete, CreatedAt = ts, LastUpdate = ts,
        };
        var beta = new Issue
        {
            Id = "id-beta", Title = "Beta", Type = IssueType.Task,
            Status = IssueStatus.Complete, CreatedAt = ts, LastUpdate = ts,
        };

        // Identical logical set written in opposite order must hash to the same archive name,
        // because the archive content is canonicalised (sorted by id) before hashing.
        var forward = await SealWrittenSetAsync([alpha, beta]);
        var reverse = await SealWrittenSetAsync([beta, alpha]);

        reverse.Should().Be(forward);
    }

    private static async Task<string> SealWrittenSetAsync(IReadOnlyList<Issue> issues)
    {
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory(BasePath);
        var services = new ServiceCollection();
        services.AddFleeceCore(BasePath, fs);
        using var provider = services.BuildServiceProvider();

        var storage = provider.GetRequiredService<IStorageService>();
        await storage.SaveIssuesAsync(issues);

        var seal = provider.GetRequiredService<ISealService>();
        var result = await seal.SealAsync();
        result.Sealed.Should().BeTrue();

        return provider.GetRequiredService<IFileSystem>().Path.GetFileName(result.ArchivePath!);
    }
}
