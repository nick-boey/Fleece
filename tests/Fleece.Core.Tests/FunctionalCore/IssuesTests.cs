using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.FunctionalCore;

[TestFixture]
public class IssuesTests
{
    #region Search Tests

    [Test]
    public void Search_FindsMatchesInTitle()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "Fix login bug", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Add feature", Status = IssueStatus.Open, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Search(issues, "login");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Search_FindsMatchesInDescription()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "Bug", Description = "Users cannot login", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Feature", Description = "Add new button", Status = IssueStatus.Open, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Search(issues, "login");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Search_FindsMatchesInTags()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "Fix bug", Tags = ["backend", "api"], Status = IssueStatus.Progress, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Add feature", Tags = ["frontend"], Status = IssueStatus.Progress, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Search(issues, "backend");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Search_FindsIssuesByKeyedTagSyntax()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "Frontend Task", Tags = ["project=frontend", "api"], Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Backend Task", Tags = ["project=backend"], Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "Other Task", Tags = ["misc"], Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Search(issues, "project:frontend");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Search_KeyedTagSearch_IsCaseInsensitive()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "Frontend Task", Tags = ["PROJECT=FRONTEND"], Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Search(issues, "project:frontend");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Search_FallsBackToSubstringSearch_WhenNoColonPattern()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "Fix login bug", Tags = ["project=frontend"], Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Add feature", Status = IssueStatus.Open, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Search(issues, "login");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Search_UsesSubstringSearch_WhenQueryHasSpaces()
    {
        // If query has spaces, it can't be a keyed tag search
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "Fix project:frontend issue", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Search(issues, "Fix project:frontend");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Search_UsesSubstringSearch_WhenColonAtStart()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "Issue with :value", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        // ":value" should not trigger keyed tag search
        var result = Issues.Search(issues, ":value");

        result.Should().HaveCount(1);
    }

    [Test]
    public void Search_UsesSubstringSearch_WhenColonAtEnd()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "Issue with key:", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        // "key:" should not trigger keyed tag search
        var result = Issues.Search(issues, "key:");

        result.Should().HaveCount(1);
    }

    #endregion

    #region Filter Tests

    [Test]
    public void Filter_FiltersByStatus()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, status: IssueStatus.Open);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Filter_FiltersByType()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, type: IssueType.Bug);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Filter_CombinesFilters()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Bug, Priority = 1, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Bug, Priority = 2, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Complete, Type = IssueType.Bug, Priority = 1, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, status: IssueStatus.Open, type: IssueType.Bug, priority: 1);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Filter_FiltersBySingleTag()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["backend", "api"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["frontend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = [], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, tags: ["backend"]);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Filter_FiltersByMultipleTags_OrLogic()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["backend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["frontend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["docs"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, tags: ["backend", "frontend"]);

        result.Should().HaveCount(2);
        result.Select(i => i.Id).Should().Contain(["a", "b"]);
    }

    [Test]
    public void Filter_FiltersByLinkedPr_FromTags()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["hsp-linked-pr=123"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["hsp-linked-pr=456"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = [], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, linkedPr: 123);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Filter_FiltersByLinkedPr_FallsBackToLegacyField()
    {
        // Test backward compatibility with deprecated LinkedPR field
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, LinkedPR = 123, Tags = [], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["hsp-linked-pr=456"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, linkedPr: 123);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Filter_TagFilterIsCaseInsensitive()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["Backend", "API"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["frontend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, tags: ["backend"]);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Filter_ExcludesTerminalStatuses_ByDefault()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Draft, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "d", Title = "D", Status = IssueStatus.Progress, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "e", Title = "E", Status = IssueStatus.Review, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "f", Title = "F", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "g", Title = "G", Status = IssueStatus.Archived, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "h", Title = "H", Status = IssueStatus.Closed, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "i", Title = "I", Status = IssueStatus.Deleted, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues);

        // Draft is non-terminal, so it should be included
        result.Should().HaveCount(5);
        result.Select(i => i.Id).Should().Contain(["a", "b", "c", "d", "e"]);
        result.Select(i => i.Id).Should().NotContain(["f", "g", "h", "i"]);
    }

    [Test]
    public void Filter_IncludesTerminalStatuses_WhenIncludeTerminalIsTrue()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Archived, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "d", Title = "D", Status = IssueStatus.Closed, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "e", Title = "E", Status = IssueStatus.Deleted, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, includeTerminal: true);

        result.Should().HaveCount(5);
        result.Select(i => i.Id).Should().Contain(["a", "b", "c", "d", "e"]);
    }

    [Test]
    public void Filter_IncludesTerminalStatus_WhenExplicitlyFiltered()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        // When a specific terminal status is requested, it should be returned even without includeTerminal
        var result = Issues.Filter(issues, status: IssueStatus.Complete);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("b");
    }

    [Test]
    public void Filter_ExcludesAllTerminalStatuses_Complete()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues);

        result.Should().BeEmpty();
    }

    [Test]
    public void Filter_ExcludesAllTerminalStatuses_Archived()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Archived, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues);

        result.Should().BeEmpty();
    }

    [Test]
    public void Filter_ExcludesAllTerminalStatuses_Closed()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Closed, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues);

        result.Should().BeEmpty();
    }

    [Test]
    public void Filter_ExcludesAllTerminalStatuses_Deleted()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Deleted, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues);

        result.Should().BeEmpty();
    }

    [Test]
    public void Filter_CombinesTerminalFilterWithOtherFilters()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Bug, Priority = 1, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Priority = 1, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Complete, Type = IssueType.Bug, Priority = 1, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, type: IssueType.Bug, priority: 1);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Filter_FiltersByTag_WhenIssueTagsIsNull()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, Tags = null!, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["backend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, tags: ["backend"]);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("b");
    }

    [Test]
    public void Filter_IncludesDraftStatus_ByDefault()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Draft, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues);

        result.Should().HaveCount(2);
        result.Select(i => i.Id).Should().Contain(["a", "b"]);
    }

    [Test]
    public void Filter_FiltersByKeyedTagWithKeyValue()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=frontend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=backend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["other"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, tags: ["project=frontend"]);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Filter_FiltersByTagKeyOnly_MatchesAllValues()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=frontend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=backend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["other"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, tags: ["project"]);

        result.Should().HaveCount(2);
        result.Select(i => i.Id).Should().BeEquivalentTo(["a", "b"]);
    }

    [Test]
    public void Filter_FiltersByMultipleKeyedTags_OrLogic()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=frontend", "priority=high"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=frontend", "priority=low"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["priority=high"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, tags: ["project=frontend", "priority=high"]);

        result.Should().HaveCount(3);
    }

    [Test]
    public void Filter_CombinesKeyedTagWithOtherFilters()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Bug, Tags = ["project=frontend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=frontend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Bug, Tags = ["project=backend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, type: IssueType.Bug, tags: ["project=frontend"]);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    [Test]
    public void Filter_TagKeyOnly_MatchesSimpleTagsToo()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["urgent"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=frontend"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["other"], LastUpdate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, tags: ["urgent"]);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a");
    }

    #endregion

    #region BuildGraph Basic Tests

    [Test]
    public void BuildGraph_WithNoIssues_ReturnsEmptyGraph()
    {
        var result = Issues.BuildGraph([]);

        result.Nodes.Should().BeEmpty();
        result.RootIssueIds.Should().BeEmpty();
    }

    [Test]
    public void BuildGraph_WithSingleIssue_ReturnsOneNode()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Open).Build();

        var result = Issues.BuildGraph([issue]);

        result.Nodes.Should().ContainSingle();
        result.Nodes["issue1"].Issue.Id.Should().Be("issue1");
        result.RootIssueIds.Should().ContainSingle().Which.Should().Be("issue1");
    }

    [Test]
    public void BuildGraph_ParentChildRelationship_CorrectChildAndParentIds()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = Issues.BuildGraph([parent, child]);

        result.Nodes.Should().HaveCount(2);

        // Parent node
        var parentNode = result.Nodes["parent"];
        parentNode.ChildIssueIds.Should().ContainSingle().Which.Should().Be("child");
        parentNode.ParentIssueIds.Should().BeEmpty();

        // Child node
        var childNode = result.Nodes["child"];
        childNode.ChildIssueIds.Should().BeEmpty();
        childNode.ParentIssueIds.Should().ContainSingle().Which.Should().Be("parent");

        // Root IDs
        result.RootIssueIds.Should().ContainSingle().Which.Should().Be("parent");
    }

    #endregion

    #region Next/Previous Computation - Series Mode

    [Test]
    public void BuildGraph_SeriesParent_FirstChildHasNoPrevious()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.BuildGraph([parent, child1, child2]);

        var child1Node = result.Nodes["child1"];
        child1Node.PreviousIssueIds.Should().BeEmpty();
        child1Node.NextIssueIds.Should().ContainSingle().Which.Should().Be("child2");
    }

    [Test]
    public void BuildGraph_SeriesParent_MiddleChildHasBothPreviousAndNext()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        var child3 = new IssueBuilder().WithId("child3").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "ccc").Build();

        var result = Issues.BuildGraph([parent, child1, child2, child3]);

        var child2Node = result.Nodes["child2"];
        child2Node.PreviousIssueIds.Should().ContainSingle().Which.Should().Be("child1");
        child2Node.NextIssueIds.Should().ContainSingle().Which.Should().Be("child3");
    }

    [Test]
    public void BuildGraph_SeriesParent_LastChildHasNoNext()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.BuildGraph([parent, child1, child2]);

        var child2Node = result.Nodes["child2"];
        child2Node.PreviousIssueIds.Should().ContainSingle().Which.Should().Be("child1");
        child2Node.NextIssueIds.Should().BeEmpty();
    }

    #endregion

    #region Next/Previous Computation - Parallel Mode

    [Test]
    public void BuildGraph_ParallelParent_AllChildrenHaveEmptyPreviousAndNext()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.BuildGraph([parent, child1, child2]);

        result.Nodes["child1"].PreviousIssueIds.Should().BeEmpty();
        result.Nodes["child1"].NextIssueIds.Should().BeEmpty();
        result.Nodes["child2"].PreviousIssueIds.Should().BeEmpty();
        result.Nodes["child2"].NextIssueIds.Should().BeEmpty();
    }

    #endregion

    #region Next/Previous Computation - Root Issues

    [Test]
    public void BuildGraph_RootIssues_HaveEmptyPreviousAndNext()
    {
        var issue1 = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Open).Build();
        var issue2 = new IssueBuilder().WithId("issue2").WithStatus(IssueStatus.Open).Build();

        var result = Issues.BuildGraph([issue1, issue2]);

        result.Nodes["issue1"].PreviousIssueIds.Should().BeEmpty();
        result.Nodes["issue1"].NextIssueIds.Should().BeEmpty();
        result.Nodes["issue2"].PreviousIssueIds.Should().BeEmpty();
        result.Nodes["issue2"].NextIssueIds.Should().BeEmpty();
    }

    #endregion

    #region Next/Previous Computation - Multi-Parent (DAG)

    [Test]
    public void BuildGraph_MultipleSeriesParents_AccumulatesPreviousFromAll()
    {
        var parent1 = new IssueBuilder().WithId("parent1").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var parent2 = new IssueBuilder().WithId("parent2").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var sibling1 = new IssueBuilder().WithId("sibling1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var multiParent = new IssueBuilder()
            .WithId("multiParent")
            .WithStatus(IssueStatus.Open)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "bbb" },
                new ParentIssueRef { ParentIssue = "parent2", SortOrder = "aaa" })
            .Build();

        var result = Issues.BuildGraph([parent1, parent2, sibling1, multiParent]);

        // multiParent should have sibling1 as previous (from parent1's series)
        var multiParentNode = result.Nodes["multiParent"];
        multiParentNode.PreviousIssueIds.Should().Contain("sibling1");
    }

    #endregion

    #region HasIncompleteChildren / AllPreviousDone

    [Test]
    public void BuildGraph_ParentWithOpenChildren_HasIncompleteChildrenIsTrue()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = Issues.BuildGraph([parent, child]);

        result.Nodes["parent"].HasIncompleteChildren.Should().BeTrue();
        result.Nodes["child"].HasIncompleteChildren.Should().BeFalse();
    }

    [Test]
    public void BuildGraph_ParentWithAllChildrenDone_HasIncompleteChildrenIsFalse()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Complete)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = Issues.BuildGraph([parent, child]);

        result.Nodes["parent"].HasIncompleteChildren.Should().BeFalse();
    }

    [Test]
    public void BuildGraph_SeriesChild_AllPreviousDoneWhenPrevComplete()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Complete)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.BuildGraph([parent, child1, child2]);

        result.Nodes["child2"].AllPreviousDone.Should().BeTrue();
    }

    [Test]
    public void BuildGraph_SeriesChild_AllPreviousDoneFalseWhenPrevOpen()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.BuildGraph([parent, child1, child2]);

        result.Nodes["child2"].AllPreviousDone.Should().BeFalse();
    }

    #endregion

    #region GetNextIssues Tests

    [Test]
    public void GetNextIssues_WithNoIssues_ReturnsEmptyList()
    {
        var result = Issues.GetNextIssues([]);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetNextIssues_WithSingleOpenIssue_ReturnsIssue()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Open).Build();

        var result = Issues.GetNextIssues([issue]);

        result.Should().ContainSingle().Which.Id.Should().Be("issue1");
    }

    [Test]
    public void GetNextIssues_WithClosedIssue_ReturnsEmptyList()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Closed).Build();

        var result = Issues.GetNextIssues([issue]);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetNextIssues_WithProgressIssue_ReturnsEmptyList()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Progress).Build();

        var result = Issues.GetNextIssues([issue]);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetNextIssues_WithReviewIssue_ReturnsIssue()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Review).Build();

        var result = Issues.GetNextIssues([issue]);

        result.Should().ContainSingle().Which.Id.Should().Be("issue1");
    }

    [Test]
    public void GetNextIssues_WithDraftIssue_ReturnsEmptyList()
    {
        // Draft issues are not actionable - they need to be fully specified first
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Draft).Build();

        var result = Issues.GetNextIssues([issue]);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetNextIssues_SeriesParent_OnlyFirstChildIsActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.GetNextIssues([parent, child1, child2]);

        result.Select(i => i.Id).Should().BeEquivalentTo(["child1"]);
    }

    [Test]
    public void GetNextIssues_SeriesParent_SecondChildActionableWhenFirstComplete()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Complete)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.GetNextIssues([parent, child1, child2]);

        result.Select(i => i.Id).Should().BeEquivalentTo(["child2"]);
    }

    [Test]
    public void GetNextIssues_ParallelParent_AllChildrenActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.GetNextIssues([parent, child1, child2]);

        result.Select(i => i.Id).Should().BeEquivalentTo(["child1", "child2"]);
    }

    [Test]
    public void GetNextIssues_ParentWithIncompleteChildren_IsNotActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = Issues.GetNextIssues([parent, child1]);

        result.Select(i => i.Id).Should().BeEquivalentTo(["child1"]);
    }

    [Test]
    public void GetNextIssues_ParentWithAllChildrenDone_IsActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Complete)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = Issues.GetNextIssues([parent, child1]);

        result.Select(i => i.Id).Should().BeEquivalentTo(["parent"]);
    }

    [Test]
    public void GetNextIssues_WithParentFilter_ReturnsOnlyDescendants()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var other = new IssueBuilder().WithId("other").WithStatus(IssueStatus.Open).Build();

        var result = Issues.GetNextIssues([parent, child1, other], parentId: "parent");

        result.Select(i => i.Id).Should().BeEquivalentTo(["child1"]);
    }

    [Test]
    public void GetNextIssues_SortsOldestFirst_ByDefault()
    {
        var olderIssue = new IssueBuilder().WithId("old1").WithTitle("Older Issue")
            .WithStatus(IssueStatus.Open).WithCreatedAt(DateTimeOffset.UtcNow.AddDays(-2)).Build();
        var newerIssue = new IssueBuilder().WithId("new1").WithTitle("Newer Issue")
            .WithStatus(IssueStatus.Open).WithCreatedAt(DateTimeOffset.UtcNow.AddDays(-1)).Build();

        var result = Issues.GetNextIssues([newerIssue, olderIssue]);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("old1");
        result[1].Id.Should().Be("new1");
    }

    [Test]
    public void GetNextIssues_SortsWithCustomConfig()
    {
        var noDesc = new IssueBuilder().WithId("noDesc").WithTitle("AAA No Description")
            .WithStatus(IssueStatus.Open).Build();
        var withDesc = new IssueBuilder().WithId("withDesc").WithTitle("ZZZ With Description")
            .WithStatus(IssueStatus.Open).WithDescription("Has a description").Build();

        var sortConfig = new GraphSortConfig
        {
            Rules = [new GraphSortRule(GraphSortCriteria.HasDescription)]
        };
        var result = Issues.GetNextIssues([noDesc, withDesc], sort: sortConfig);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("withDesc");
        result[1].Id.Should().Be("noDesc");
    }

    [Test]
    public void GetNextIssues_SortsByPriorityDescending_WhenConfigured()
    {
        var lowPriority = new IssueBuilder().WithId("low").WithTitle("Low Priority")
            .WithStatus(IssueStatus.Open).WithPriority(5)
            .WithCreatedAt(DateTimeOffset.UtcNow.AddDays(-2)).Build();
        var highPriority = new IssueBuilder().WithId("high").WithTitle("High Priority")
            .WithStatus(IssueStatus.Open).WithPriority(1)
            .WithCreatedAt(DateTimeOffset.UtcNow.AddDays(-1)).Build();

        var sortConfig = new GraphSortConfig
        {
            Rules = [new GraphSortRule(GraphSortCriteria.Priority, SortDirection.Descending)]
        };
        var result = Issues.GetNextIssues([lowPriority, highPriority], sort: sortConfig);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("low"); // Priority 5 first when descending
        result[1].Id.Should().Be("high");
    }

    [Test]
    public void GetNextIssues_SortsByTitleAscending_WhenConfigured()
    {
        var issueB = new IssueBuilder().WithId("b").WithTitle("Banana")
            .WithStatus(IssueStatus.Open).Build();
        var issueA = new IssueBuilder().WithId("a").WithTitle("Apple")
            .WithStatus(IssueStatus.Open).Build();

        var sortConfig = new GraphSortConfig
        {
            Rules = [new GraphSortRule(GraphSortCriteria.Title)]
        };
        var result = Issues.GetNextIssues([issueB, issueA], sort: sortConfig);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("a");
        result[1].Id.Should().Be("b");
    }

    [Test]
    public void GetNextIssues_SortsByMultipleCriteria()
    {
        var issue1 = new IssueBuilder().WithId("a").WithTitle("Alpha")
            .WithStatus(IssueStatus.Open).WithPriority(1).Build();
        var issue2 = new IssueBuilder().WithId("b").WithTitle("Bravo")
            .WithStatus(IssueStatus.Open).WithPriority(1).Build();
        var issue3 = new IssueBuilder().WithId("c").WithTitle("Charlie")
            .WithStatus(IssueStatus.Open).WithPriority(2).Build();

        var sortConfig = new GraphSortConfig
        {
            Rules =
            [
                new GraphSortRule(GraphSortCriteria.Priority),
                new GraphSortRule(GraphSortCriteria.Title)
            ]
        };
        var result = Issues.GetNextIssues([issue3, issue2, issue1], sort: sortConfig);

        result.Should().HaveCount(3);
        result[0].Id.Should().Be("a"); // Priority 1, Alpha
        result[1].Id.Should().Be("b"); // Priority 1, Bravo
        result[2].Id.Should().Be("c"); // Priority 2
    }

    #endregion


    #region QueryGraph Tests

    [Test]
    public void QueryGraph_StatusFilter_ReturnsOnlyMatchingStatus()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var review = new IssueBuilder().WithId("review1").WithStatus(IssueStatus.Review).Build();

        var result = Issues.QueryGraph([open, review], new GraphQuery { Status = IssueStatus.Open });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("open1");
    }

    [Test]
    public void QueryGraph_TypeFilter_ReturnsOnlyMatchingType()
    {
        var task = new IssueBuilder().WithId("task1").WithType(IssueType.Task).Build();
        var bug = new IssueBuilder().WithId("bug1").WithType(IssueType.Bug).Build();

        var result = Issues.QueryGraph([task, bug], new GraphQuery { Type = IssueType.Bug });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("bug1");
    }

    [Test]
    public void QueryGraph_TagsFilter_ReturnsMatchingTags()
    {
        var tagged = new IssueBuilder().WithId("tagged1").WithTags("important", "urgent").Build();
        var untagged = new IssueBuilder().WithId("untagged1").Build();

        var result = Issues.QueryGraph([tagged, untagged], new GraphQuery { Tags = ["important"] });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("tagged1");
    }

    [Test]
    public void QueryGraph_SearchText_MatchesTitle()
    {
        var matches = new IssueBuilder().WithId("match1").WithTitle("Fix the login bug").Build();
        var noMatch = new IssueBuilder().WithId("nomatch1").WithTitle("Add new feature").Build();

        var result = Issues.QueryGraph([matches, noMatch], new GraphQuery { SearchText = "login" });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("match1");
    }

    [Test]
    public void QueryGraph_SearchText_MatchesDescription()
    {
        var matches = new IssueBuilder().WithId("match1").WithTitle("Issue").WithDescription("The login is broken").Build();
        var noMatch = new IssueBuilder().WithId("nomatch1").WithTitle("Issue2").Build();

        var result = Issues.QueryGraph([matches, noMatch], new GraphQuery { SearchText = "login" });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("match1");
    }

    [Test]
    public void QueryGraph_RootIssueId_ReturnsDescendantsAndRoot()
    {
        var root = new IssueBuilder().WithId("root").WithStatus(IssueStatus.Open).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("root", "aaa").Build();
        var other = new IssueBuilder().WithId("other").WithStatus(IssueStatus.Open).Build();

        var result = Issues.QueryGraph([root, child, other], new GraphQuery { RootIssueId = "root" });

        result.Nodes.Should().HaveCount(2);
        result.Nodes.Should().ContainKey("root");
        result.Nodes.Should().ContainKey("child");
        result.Nodes.Should().NotContainKey("other");
    }

    [Test]
    public void QueryGraph_IncludeTerminal_IncludesTerminalIssues()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();

        var result = Issues.QueryGraph([open, complete], new GraphQuery { IncludeTerminal = true });

        result.Nodes.Should().HaveCount(2);
    }

    [Test]
    public void QueryGraph_ExcludesTerminalByDefault()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();

        var result = Issues.QueryGraph([open, complete], new GraphQuery());

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("open1");
    }

    [Test]
    public void QueryGraph_IncludeInactiveWithActiveDescendants_IncludesTerminalParent()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Complete).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = Issues.QueryGraph([parent, child], new GraphQuery
        {
            IncludeInactiveWithActiveDescendants = true
        });

        result.Nodes.Should().HaveCount(2);
        result.Nodes.Should().ContainKey("parent");
        result.Nodes.Should().ContainKey("child");
    }

    [Test]
    public void QueryGraph_AssignedToFilter_ReturnsMatchingAssignee()
    {
        var assigned = new IssueBuilder().WithId("assigned1").WithAssignedTo("john").Build();
        var other = new IssueBuilder().WithId("other1").WithAssignedTo("jane").Build();

        var result = Issues.QueryGraph([assigned, other], new GraphQuery { AssignedTo = "john" });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("assigned1");
    }

    [Test]
    public void QueryGraph_PriorityFilter_ReturnsMatchingPriority()
    {
        var highPri = new IssueBuilder().WithId("high1").WithPriority(1).Build();
        var lowPri = new IssueBuilder().WithId("low1").WithPriority(5).Build();

        var result = Issues.QueryGraph([highPri, lowPri], new GraphQuery { Priority = 1 });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("high1");
    }

    [Test]
    public void QueryGraph_LinkedPrFilter_ReturnsMatchingPR()
    {
        var withPr = new IssueBuilder().WithId("pr1").WithLinkedPr(123).Build();
        var noPr = new IssueBuilder().WithId("nopr1").Build();

        var result = Issues.QueryGraph([withPr, noPr], new GraphQuery { LinkedPr = 123 });

        result.Nodes.Values.Should().ContainSingle().Which.Issue.Id.Should().Be("pr1");
    }

    [Test]
    public void QueryGraph_FilteredSubgraph_PreservesNextPreviousFromFullGraph()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();

        // Even when filtered, Next/Previous should reflect full graph
        var result = Issues.QueryGraph([parent, child1, child2], new GraphQuery());

        result.Nodes["child1"].NextIssueIds.Should().Contain("child2");
        result.Nodes["child2"].PreviousIssueIds.Should().Contain("child1");
    }

    #endregion

    #region ParentExecutionMode Tests

    [Test]
    public void BuildGraph_RootIssue_HasNullParentExecutionMode()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Open).Build();

        var result = Issues.BuildGraph([issue]);

        result.Nodes["issue1"].ParentExecutionMode.Should().BeNull();
    }

    [Test]
    public void BuildGraph_ChildOfSeriesParent_HasSeriesParentExecutionMode()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Series).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = Issues.BuildGraph([parent, child]);

        result.Nodes["child"].ParentExecutionMode.Should().Be(ExecutionMode.Series);
    }

    [Test]
    public void BuildGraph_ChildOfParallelParent_HasParallelParentExecutionMode()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = Issues.BuildGraph([parent, child]);

        result.Nodes["child"].ParentExecutionMode.Should().Be(ExecutionMode.Parallel);
    }

    #endregion

    #region Idea Type Exclusion Tests

    [Test]
    public void GetNextIssues_WithIdeaTypeIssue_ExcludesIdea()
    {
        var idea = new IssueBuilder().WithId("idea1").WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();

        var result = Issues.GetNextIssues([idea]);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetNextIssues_WithMixedTypeIssues_ExcludesOnlyIdeas()
    {
        var task = new IssueBuilder().WithId("task1").WithStatus(IssueStatus.Open).WithType(IssueType.Task).Build();
        var bug = new IssueBuilder().WithId("bug1").WithStatus(IssueStatus.Open).WithType(IssueType.Bug).Build();
        var idea = new IssueBuilder().WithId("idea1").WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();
        var feature = new IssueBuilder().WithId("feature1").WithStatus(IssueStatus.Open).WithType(IssueType.Feature).Build();

        var result = Issues.GetNextIssues([task, bug, idea, feature]);

        result.Select(i => i.Id).Should().BeEquivalentTo(["task1", "bug1", "feature1"]);
        result.Select(i => i.Id).Should().NotContain("idea1");
    }

    [Test]
    public void GetNextIssues_WithIdeaInReviewStatus_StillExcludesIdea()
    {
        var idea = new IssueBuilder().WithId("idea1").WithStatus(IssueStatus.Review).WithType(IssueType.Idea).Build();
        var task = new IssueBuilder().WithId("task1").WithStatus(IssueStatus.Review).WithType(IssueType.Task).Build();

        var result = Issues.GetNextIssues([idea, task]);

        result.Should().ContainSingle().Which.Id.Should().Be("task1");
    }

    [Test]
    public void GetNextIssues_ParentWithIdeaChild_IdeaChildNotActionable()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var taskChild = new IssueBuilder().WithId("task-child").WithStatus(IssueStatus.Open).WithType(IssueType.Task).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var ideaChild = new IssueBuilder().WithId("idea-child").WithStatus(IssueStatus.Open).WithType(IssueType.Idea).WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.GetNextIssues([parent, taskChild, ideaChild]);

        result.Select(i => i.Id).Should().BeEquivalentTo(["task-child"]);
    }

    #endregion




    #region Edge Cases

    [Test]
    public void BuildGraph_OrphanIssue_TreatedAsRoot()
    {
        var orphan = new IssueBuilder().WithId("orphan").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("nonexistent", "aaa").Build();

        var result = Issues.BuildGraph([orphan]);

        result.Nodes.Should().ContainSingle();
        result.RootIssueIds.Should().ContainSingle().Which.Should().Be("orphan");
        // Parent doesn't exist in graph, so ParentIssueIds should be empty
        result.Nodes["orphan"].ParentIssueIds.Should().BeEmpty();
    }

    [Test]
    public void GetNextIssues_NestedHierarchy_RespectsParentExecutionModes()
    {
        var grandparent = new IssueBuilder().WithId("grandparent").WithStatus(IssueStatus.Open)
            .WithExecutionMode(ExecutionMode.Parallel).Build();
        var parent1 = new IssueBuilder().WithId("parent1").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("grandparent", "aaa").WithExecutionMode(ExecutionMode.Series).Build();
        var parent2 = new IssueBuilder().WithId("parent2").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("grandparent", "bbb").WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1a = new IssueBuilder().WithId("child1a").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var child1b = new IssueBuilder().WithId("child1b").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent1", "bbb").Build();
        var child2a = new IssueBuilder().WithId("child2a").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent2", "aaa").Build();
        var child2b = new IssueBuilder().WithId("child2b").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent2", "bbb").Build();

        var result = Issues.GetNextIssues([grandparent, parent1, parent2, child1a, child1b, child2a, child2b]);

        // parent1 is series, so only child1a is actionable (not child1b)
        // parent2 is parallel, so both child2a and child2b are actionable
        result.Select(i => i.Id).Should().BeEquivalentTo(["child1a", "child2a", "child2b"]);
    }

    #endregion
}
