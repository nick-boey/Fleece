using System.IO.Abstractions;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Extensions;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Testably.Abstractions.Testing;

namespace Fleece.Core.Tests.EventSourcing;

/// <summary>
/// Storage tests for the v4 per-issue append-only log model: each issue is one
/// <c>.fleece/issues/&lt;id&gt;.jsonl</c> file, replayed independently. There is no snapshot,
/// no <c>.fleece/changes/</c> directory, and no cross-file ordering.
/// </summary>
[TestFixture]
public sealed class PerIssueLogTests
{
    private const string BasePath = "/project";

    private MockFileSystem _fs = null!;
    private ServiceProvider _provider = null!;
    private IFleeceService _fleece = null!;
    private IEventStore _eventStore = null!;
    private IStorageService _storage = null!;

    private string IssuesDir => _fs.Path.Combine(BasePath, ".fleece", "issues");
    private string LogPathFor(string id) => _fs.Path.Combine(IssuesDir, $"{id}.jsonl");

    [SetUp]
    public void SetUp()
    {
        _fs = new MockFileSystem();
        _fs.Directory.CreateDirectory(BasePath);

        var services = new ServiceCollection();
        services.AddFleeceCore(BasePath, _fs);
        _provider = services.BuildServiceProvider();

        _fleece = _provider.GetRequiredService<IFleeceService>();
        _eventStore = _provider.GetRequiredService<IEventStore>();
        _storage = _provider.GetRequiredService<IStorageService>();
    }

    [TearDown]
    public void TearDown() => _provider.Dispose();

    [Test]
    public async Task Create_writes_one_log_file_per_issue_named_by_id()
    {
        var a = await _fleece.CreateAsync("First", IssueType.Task);
        var b = await _fleece.CreateAsync("Second", IssueType.Bug);

        _fs.File.Exists(LogPathFor(a.Id)).Should().BeTrue();
        _fs.File.Exists(LogPathFor(b.Id)).Should().BeTrue();
        _fs.Directory.GetFiles(IssuesDir, "*.jsonl").Should().HaveCount(2);

        // No snapshot, no changes directory.
        _fs.File.Exists(_fs.Path.Combine(BasePath, ".fleece", "issues.jsonl")).Should().BeFalse();
        _fs.Directory.Exists(_fs.Path.Combine(BasePath, ".fleece", "changes")).Should().BeFalse();
    }

    [Test]
    public async Task Log_first_line_is_create_then_set_events_on_edit()
    {
        var a = await _fleece.CreateAsync("Title A", IssueType.Task);
        await _fleece.UpdateAsync(a.Id, title: "Title B");

        var events = await _eventStore.ReadIssueLogAsync(LogPathFor(a.Id));
        events.Should().HaveCountGreaterThanOrEqualTo(2);
        events[0].Kind.Should().Be("create");
        events.Skip(1).Should().Contain(e => e.Kind == "set");

        // Still a single file for the issue after the edit.
        _fs.Directory.GetFiles(IssuesDir, "*.jsonl").Should().HaveCount(1);
    }

    [Test]
    public async Task Edit_replays_to_latest_value()
    {
        var a = await _fleece.CreateAsync("Original", IssueType.Task);
        await _fleece.UpdateAsync(a.Id, title: "Updated");

        var reloaded = await _fleece.GetByIdAsync(a.Id);
        reloaded!.Title.Should().Be("Updated");
    }

    [Test]
    public async Task Status_set_change_persists_and_replays()
    {
        var a = await _fleece.CreateAsync("S", IssueType.Task, status: IssueStatus.Open);
        await _fleece.UpdateAsync(a.Id, status: IssueStatus.Progress);

        (await _fleece.GetByIdAsync(a.Id))!.Status.Should().Be(IssueStatus.Progress);

        await _fleece.UpdateAsync(a.Id, status: IssueStatus.Complete);
        (await _fleece.GetByIdAsync(a.Id))!.Status.Should().Be(IssueStatus.Complete);
    }

    [Test]
    public async Task Type_set_change_persists_and_replays()
    {
        var a = await _fleece.CreateAsync("T", IssueType.Task);
        await _fleece.UpdateAsync(a.Id, type: IssueType.Feature);

        (await _fleece.GetByIdAsync(a.Id))!.Type.Should().Be(IssueType.Feature);
    }

    [Test]
    public async Task Files_replay_independently()
    {
        var a = await _fleece.CreateAsync("A", IssueType.Task);
        var b = await _fleece.CreateAsync("B", IssueType.Task);
        await _fleece.UpdateAsync(b.Id, title: "B-updated");

        var all = await _fleece.GetAllAsync();
        all.Should().HaveCount(2);
        all.Single(i => i.Id == a.Id).Title.Should().Be("A");
        all.Single(i => i.Id == b.Id).Title.Should().Be("B-updated");
    }

    [Test]
    public async Task Dropping_an_issue_from_a_save_deletes_its_log_file()
    {
        var a = await _fleece.CreateAsync("Keep", IssueType.Task);
        var b = await _fleece.CreateAsync("Drop", IssueType.Task);

        _fs.File.Exists(LogPathFor(b.Id)).Should().BeTrue();

        // Persisting a set that no longer contains b removes its per-issue log file.
        await _storage.SaveIssuesAsync([a]);

        _fs.File.Exists(LogPathFor(b.Id)).Should().BeFalse();
        _fs.File.Exists(LogPathFor(a.Id)).Should().BeTrue();
    }

    [Test]
    public async Task DeleteIssueLog_removes_the_file()
    {
        var a = await _fleece.CreateAsync("Gone", IssueType.Task);
        _fs.File.Exists(LogPathFor(a.Id)).Should().BeTrue();

        await _eventStore.DeleteIssueLogAsync(a.Id);

        _fs.File.Exists(LogPathFor(a.Id)).Should().BeFalse();
    }
}
