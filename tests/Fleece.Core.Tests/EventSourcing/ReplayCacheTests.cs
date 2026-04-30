using Fleece.Core.EventSourcing.Services;
using Fleece.Core.Models;
using FluentAssertions;
using NUnit.Framework;
using Testably.Abstractions.Testing;
using Issue = Fleece.Core.EventSourcing.Issue;

namespace Fleece.Core.Tests.EventSourcing;

[TestFixture]
public sealed class ReplayCacheTests
{
    private MockFileSystem _fs = null!;
    private ReplayCache _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _fs = new MockFileSystem();
        _fs.Directory.CreateDirectory("/repo");
        _sut = new ReplayCache("/repo", _fs);
    }

    private static Issue Make(string id) => new()
    {
        Id = id,
        Title = id,
        Status = IssueStatus.Open,
        Type = IssueType.Task,
        CreatedAt = DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
        LastUpdate = DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
    };

    [Test]
    public async Task TryRead_NoFile_ReturnsNull()
    {
        var result = await _sut.TryReadAsync();
        result.Should().BeNull();
    }

    [Test]
    public async Task WriteThenRead_RoundTrips()
    {
        var state = new Dictionary<string, Issue>
        {
            ["a"] = Make("a"),
            ["b"] = Make("b"),
        };
        await _sut.WriteAsync("sha-123", state);
        var loaded = await _sut.TryReadAsync();
        loaded.Should().NotBeNull();
        loaded!.HeadSha.Should().Be("sha-123");
        loaded.Issues.Select(i => i.Id).Should().BeEquivalentTo(["a", "b"]);
    }

    [Test]
    public async Task TryRead_CorruptFile_ReturnsNull()
    {
        var dir = _fs.Path.Combine("/repo", ".fleece");
        _fs.Directory.CreateDirectory(dir);
        await _fs.File.WriteAllTextAsync(_fs.Path.Combine(dir, ".replay-cache"), "{not valid json");
        var result = await _sut.TryReadAsync();
        result.Should().BeNull();
    }

    [Test]
    public async Task Invalidate_DeletesFile()
    {
        await _sut.WriteAsync("sha", new Dictionary<string, Issue>());
        await _sut.InvalidateAsync();
        var result = await _sut.TryReadAsync();
        result.Should().BeNull();
    }
}
