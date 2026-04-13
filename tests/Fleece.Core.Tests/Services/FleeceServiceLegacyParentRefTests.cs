using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

/// <summary>
/// Regression tests for legacy parent-ref JSONL shapes (pre per-ref timestamp / Active fields).
///
/// Old JSONL files contain parent references like <c>{"parentIssue":"X","sortOrder":"0"}</c>
/// without the <c>active</c>, <c>lastUpdated</c>, or <c>updatedBy</c> fields. System.Text.Json
/// source-gen does not honor the <c>= true</c> initializer on <c>ParentIssueRef.Active</c> in
/// this case — those refs deserialize with <c>Active = false</c>, which makes them vanish from
/// <see cref="Fleece.Core.Models.Issue.ActiveParentIssues"/> and collapses tree/next rendering
/// to a flat list. <see cref="FleeceService"/> must auto-repair this on load.
/// </summary>
[TestFixture]
public class FleeceServiceLegacyParentRefTests
{
    private string _tempDir = null!;
    private string _fleecePath = null!;
    private FleeceService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"fleece-legacy-parent-{Guid.NewGuid()}");
        _fleecePath = Path.Combine(_tempDir, ".fleece");
        Directory.CreateDirectory(_fleecePath);

        var serializer = new JsonlSerializer();
        var schemaValidator = new SchemaValidator();
        var storage = new JsonlStorageService(_tempDir, serializer, schemaValidator);
        var gitConfig = Substitute.For<IGitConfigService>();
        gitConfig.GetUserName().Returns("test-user");
        var settings = Substitute.For<ISettingsService>();
        var idGenerator = new GuidIdGenerator();

        _sut = new FleeceService(storage, idGenerator, gitConfig, settings);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public async Task GetAllAsync_LegacyParentRefWithoutActiveField_IsRepairedOnLoad()
    {
        // Hand-written JSONL mimicking the pre-PR-#113 on-disk shape: parent refs lack
        // `active`, `lastUpdated`, and `updatedBy`. If loaded naively, System.Text.Json
        // would set Active=false and the parent relationship would disappear.
        const string legacyJsonl = """
            {"id":"PARENT-1","title":"Parent","titleLastUpdate":"2026-02-06T10:00:00+00:00","status":"Open","statusLastUpdate":"2026-02-06T10:00:00+00:00","type":"Task","typeLastUpdate":"2026-02-06T10:00:00+00:00","linkedIssues":[],"linkedIssuesLastUpdate":"2026-02-06T10:00:00+00:00","parentIssues":[],"tags":[],"tagsLastUpdate":"0001-01-01T00:00:00+00:00","executionMode":"Series","lastUpdate":"2026-02-06T10:00:00+00:00","createdAt":"2026-02-06T10:00:00+00:00"}
            {"id":"CHILD-1","title":"Child","titleLastUpdate":"2026-02-07T10:00:00+00:00","status":"Open","statusLastUpdate":"2026-02-07T10:00:00+00:00","type":"Task","typeLastUpdate":"2026-02-07T10:00:00+00:00","linkedIssues":[],"linkedIssuesLastUpdate":"2026-02-07T10:00:00+00:00","parentIssues":[{"parentIssue":"PARENT-1","sortOrder":"0"}],"tags":[],"tagsLastUpdate":"0001-01-01T00:00:00+00:00","executionMode":"Series","lastUpdate":"2026-02-07T10:00:00+00:00","createdAt":"2026-02-07T10:00:00+00:00"}

            """;
        await File.WriteAllTextAsync(Path.Combine(_fleecePath, "issues_legacy.jsonl"), legacyJsonl);

        var issues = await _sut.GetAllAsync();

        var child = issues.Single(i => i.Id == "CHILD-1");
        child.ParentIssues.Should().ContainSingle();
        child.ActiveParentIssues.Should().ContainSingle()
            .Which.ParentIssue.Should().Be("PARENT-1");

        // Migration also stamps a non-default LastUpdated for the parent ref.
        child.ActiveParentIssues[0].LastUpdated.Should().NotBe(default);
        child.ActiveParentIssues[0].Active.Should().BeTrue();
    }
}
