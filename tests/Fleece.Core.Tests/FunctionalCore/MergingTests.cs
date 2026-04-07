using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.FunctionalCore;

[TestFixture]
public class MergingTests
{
    [Test]
    public void Plan_ReturnsNoDuplicates_WhenAllIssuesAreUnique()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("a").WithTitle("A").Build(),
            new IssueBuilder().WithId("b").WithTitle("B").Build()
        };
        var fileGroups = new List<(string, IReadOnlyList<Issue>)>
        {
            ("/mock/issues_abc123.jsonl", issues)
        };

        var plan = Merging.Plan(fileGroups, "Test User");

        plan.DuplicateCount.Should().Be(0);
    }

    [Test]
    public void Plan_DetectsDuplicates_WhenIssuesShareSameId()
    {
        var older = DateTimeOffset.UtcNow.AddHours(-1);
        var newer = DateTimeOffset.UtcNow;

        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("abc123").WithTitle("Old").WithLastUpdate(older).Build(),
            new IssueBuilder().WithId("abc123").WithTitle("New").WithLastUpdate(newer).Build()
        };
        var fileGroups = new List<(string, IReadOnlyList<Issue>)>
        {
            ("/mock/issues_abc123.jsonl", issues)
        };

        var plan = Merging.Plan(fileGroups, "Test User");

        plan.DuplicateCount.Should().Be(1);
    }

    [Test]
    public void Apply_KeepsNewerVersion_WhenDuplicatesExist()
    {
        var older = DateTimeOffset.UtcNow.AddHours(-1);
        var newer = DateTimeOffset.UtcNow;

        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("abc123").WithTitle("Old").WithLastUpdate(older).Build(),
            new IssueBuilder().WithId("abc123").WithTitle("New").WithLastUpdate(newer).Build()
        };
        var fileGroups = new List<(string, IReadOnlyList<Issue>)>
        {
            ("/mock/issues_abc123.jsonl", issues)
        };

        var plan = Merging.Plan(fileGroups, "Test User");
        var result = Merging.Apply(plan);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("New");
    }

    [Test]
    public void Plan_HandlesMultipleCopies_AsOneDuplicateGroup()
    {
        var time1 = DateTimeOffset.UtcNow.AddHours(-2);
        var time2 = DateTimeOffset.UtcNow.AddHours(-1);
        var time3 = DateTimeOffset.UtcNow;

        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("abc123").WithTitle("V1").WithLastUpdate(time1).Build(),
            new IssueBuilder().WithId("abc123").WithTitle("V2").WithLastUpdate(time2).Build(),
            new IssueBuilder().WithId("abc123").WithTitle("V3").WithLastUpdate(time3).Build()
        };
        var fileGroups = new List<(string, IReadOnlyList<Issue>)>
        {
            ("/mock/issues_abc123.jsonl", issues)
        };

        var plan = Merging.Plan(fileGroups, "Test User");

        plan.DuplicateCount.Should().Be(1);
    }
}
