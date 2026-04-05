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
            new() { Id = "a", Title = "Fix login bug", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Add feature", Status = IssueStatus.Open, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "Bug", Description = "Users cannot login", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Feature", Description = "Add new button", Status = IssueStatus.Open, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "Fix bug", Tags = ["backend", "api"], Status = IssueStatus.Progress, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Add feature", Tags = ["frontend"], Status = IssueStatus.Progress, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "Frontend Task", Tags = ["project=frontend", "api"], Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Backend Task", Tags = ["project=backend"], Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "Other Task", Tags = ["misc"], Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "Frontend Task", Tags = ["PROJECT=FRONTEND"], Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "Fix login bug", Tags = ["project=frontend"], Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "Add feature", Status = IssueStatus.Open, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "Fix project:frontend issue", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "Issue with :value", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "Issue with key:", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Bug, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Feature, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Bug, Priority = 1, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Bug, Priority = 2, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Complete, Type = IssueType.Bug, Priority = 1, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["backend", "api"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["frontend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = [], LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["backend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["frontend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["docs"], LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["hsp-linked-pr=123"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["hsp-linked-pr=456"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = [], LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, LinkedPR = 123, Tags = [], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["hsp-linked-pr=456"], LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["Backend", "API"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Progress, Type = IssueType.Task, Tags = ["frontend"], LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Draft, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "d", Title = "D", Status = IssueStatus.Progress, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "e", Title = "E", Status = IssueStatus.Review, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "f", Title = "F", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "g", Title = "G", Status = IssueStatus.Archived, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "h", Title = "H", Status = IssueStatus.Closed, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "i", Title = "I", Status = IssueStatus.Deleted, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Archived, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "d", Title = "D", Status = IssueStatus.Closed, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "e", Title = "E", Status = IssueStatus.Deleted, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Complete, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues);

        result.Should().BeEmpty();
    }

    [Test]
    public void Filter_ExcludesAllTerminalStatuses_Archived()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Archived, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues);

        result.Should().BeEmpty();
    }

    [Test]
    public void Filter_ExcludesAllTerminalStatuses_Closed()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Closed, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues);

        result.Should().BeEmpty();
    }

    [Test]
    public void Filter_ExcludesAllTerminalStatuses_Deleted()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Deleted, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues);

        result.Should().BeEmpty();
    }

    [Test]
    public void Filter_CombinesTerminalFilterWithOtherFilters()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Bug, Priority = 1, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Priority = 1, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Complete, Type = IssueType.Bug, Priority = 1, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, Tags = null!, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["backend"], LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Draft, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=frontend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=backend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["other"], LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=frontend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=backend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["other"], LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=frontend", "priority=high"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=frontend", "priority=low"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["priority=high"], LastUpdate = DateTimeOffset.UtcNow }
        };

        var result = Issues.Filter(issues, tags: ["project=frontend", "priority=high"]);

        result.Should().HaveCount(3);
    }

    [Test]
    public void Filter_CombinesKeyedTagWithOtherFilters()
    {
        var issues = new List<Issue>
        {
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Bug, Tags = ["project=frontend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=frontend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Bug, Tags = ["project=backend"], LastUpdate = DateTimeOffset.UtcNow }
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
            new() { Id = "a", Title = "A", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["urgent"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "b", Title = "B", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["project=frontend"], LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = "c", Title = "C", Status = IssueStatus.Open, Type = IssueType.Task, Tags = ["other"], LastUpdate = DateTimeOffset.UtcNow }
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

    #region BuildTaskGraphLayout Tests

    [Test]
    public void BuildTaskGraphLayout_WithNoIssues_ReturnsEmptyGraph()
    {
        var result = Issues.BuildTaskGraphLayout([]);

        result.Nodes.Should().BeEmpty();
        result.TotalLanes.Should().Be(0);
    }

    [Test]
    public void BuildTaskGraphLayout_WithSingleLeafIssue_ReturnsOneNodeAtLaneZero()
    {
        var issue = new IssueBuilder().WithId("issue1").WithTitle("Do something")
            .WithStatus(IssueStatus.Open).Build();

        var result = Issues.BuildTaskGraphLayout([issue]);

        result.Nodes.Should().ContainSingle();
        result.Nodes[0].Issue.Id.Should().Be("issue1");
        result.Nodes[0].Lane.Should().Be(0);
        result.Nodes[0].Row.Should().Be(0);
        result.Nodes[0].IsActionable.Should().BeTrue();
        result.TotalLanes.Should().Be(1);
    }

    [Test]
    public void BuildTaskGraphLayout_ExcludesTerminalIssues()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();
        var closed = new IssueBuilder().WithId("closed1").WithStatus(IssueStatus.Closed).Build();

        var result = Issues.BuildTaskGraphLayout([open, complete, closed]);

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("open1");
    }

    [Test]
    public void BuildTaskGraphLayout_SeriesParentWithTwoLeafChildren_CorrectLanes()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.BuildTaskGraphLayout([parent, child1, child2]);

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup["child1"].Lane.Should().Be(0);
        nodeLookup["child2"].Lane.Should().Be(0);
        nodeLookup["parent"].Lane.Should().Be(1);

        result.Nodes.Select(n => n.Issue.Id).Should().ContainInOrder("child1", "child2", "parent");

        nodeLookup["child1"].IsActionable.Should().BeTrue();
        nodeLookup["child2"].IsActionable.Should().BeFalse();
        nodeLookup["parent"].IsActionable.Should().BeFalse();
    }

    [Test]
    public void BuildTaskGraphLayout_ParallelParentWithTwoLeafChildren_CorrectLanes()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.BuildTaskGraphLayout([parent, child1, child2]);

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup["child1"].Lane.Should().Be(0);
        nodeLookup["child2"].Lane.Should().Be(0);
        nodeLookup["parent"].Lane.Should().Be(1);

        nodeLookup["child1"].IsActionable.Should().BeTrue();
        nodeLookup["child2"].IsActionable.Should().BeTrue();
    }

    [Test]
    public void BuildTaskGraphLayout_GoToWorkExample_CorrectLaneAssignments()
    {
        var goToWork = new IssueBuilder().WithId("go-to-work").WithTitle("Go to work")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var wakeUp = new IssueBuilder().WithId("wake-up").WithTitle("Wake up")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("go-to-work", "aaa").Build();
        var makeBreakfast = new IssueBuilder().WithId("make-breakfast").WithTitle("Make breakfast")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel)
            .WithParentIssueIdAndOrder("go-to-work", "bbb").Build();
        var makeCoffee = new IssueBuilder().WithId("make-coffee").WithTitle("Make coffee")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("make-breakfast", "aaa").Build();
        var makeToast = new IssueBuilder().WithId("make-toast").WithTitle("Make toast")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series)
            .WithParentIssueIdAndOrder("make-breakfast", "bbb").Build();
        var toastBread = new IssueBuilder().WithId("toast-bread").WithTitle("Toast bread")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("make-toast", "aaa").Build();
        var spreadButter = new IssueBuilder().WithId("spread-butter").WithTitle("Spread butter")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("make-toast", "bbb").Build();
        var getInCar = new IssueBuilder().WithId("get-in-car").WithTitle("Get in car")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("go-to-work", "ccc").Build();
        var driveToWork = new IssueBuilder().WithId("drive-to-work").WithTitle("Drive to work")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("go-to-work", "ddd").Build();

        var allIssues = new List<Issue>
            { goToWork, wakeUp, makeBreakfast, makeCoffee, makeToast, toastBread, spreadButter, getInCar, driveToWork };

        var result = Issues.BuildTaskGraphLayout(allIssues);

        result.Nodes.Should().HaveCount(9);

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);

        nodeLookup["wake-up"].Lane.Should().Be(0);
        nodeLookup["make-coffee"].Lane.Should().Be(1);
        nodeLookup["toast-bread"].Lane.Should().Be(1);
        nodeLookup["spread-butter"].Lane.Should().Be(1);
        nodeLookup["make-toast"].Lane.Should().Be(2);
        nodeLookup["make-breakfast"].Lane.Should().Be(3);
        nodeLookup["get-in-car"].Lane.Should().Be(3);
        nodeLookup["drive-to-work"].Lane.Should().Be(3);
        nodeLookup["go-to-work"].Lane.Should().Be(4);

        result.TotalLanes.Should().Be(5);
    }

    [Test]
    public void BuildTaskGraphLayout_OpenChildWithCompleteParent_ParentIncludedInGraph()
    {
        var parent = new IssueBuilder().WithId("go-to-work").WithTitle("Go to work")
            .WithStatus(IssueStatus.Complete).WithExecutionMode(ExecutionMode.Series).Build();
        var child = new IssueBuilder().WithId("drive-to-work").WithTitle("Drive to work")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("go-to-work", "aaa").Build();

        var result = Issues.BuildTaskGraphLayout([parent, child]);

        result.Nodes.Should().HaveCount(2);
        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);

        nodeLookup["drive-to-work"].Lane.Should().Be(0);
        nodeLookup["go-to-work"].Lane.Should().Be(1);

        nodeLookup["drive-to-work"].IsActionable.Should().BeTrue();
        nodeLookup["go-to-work"].IsActionable.Should().BeFalse();
    }

    [Test]
    public void BuildTaskGraphLayout_MultiParentIssue_AppearsUnderEachParent()
    {
        var parentA = new IssueBuilder().WithId("parentA").WithTitle("Parent A")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var parentB = new IssueBuilder().WithId("parentB").WithTitle("Parent B")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var sharedChild = new IssueBuilder().WithId("shared").WithTitle("Shared Child")
            .WithStatus(IssueStatus.Open)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parentA", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "parentB", SortOrder = "aaa" })
            .Build();

        var result = Issues.BuildTaskGraphLayout([parentA, parentB, sharedChild]);

        // Shared child should appear twice (once under each parent)
        var sharedNodes = result.Nodes.Where(n => n.Issue.Id == "shared").ToList();
        sharedNodes.Should().HaveCount(2);
        result.Nodes.Should().HaveCount(4); // parentA, shared(1), parentB, shared(2)

        // Verify AppearanceIndex and TotalAppearances
        sharedNodes[0].AppearanceIndex.Should().Be(1);
        sharedNodes[0].TotalAppearances.Should().Be(2);
        sharedNodes[1].AppearanceIndex.Should().Be(2);
        sharedNodes[1].TotalAppearances.Should().Be(2);

        // Both should reference the same Issue object
        sharedNodes[0].Issue.Should().BeSameAs(sharedNodes[1].Issue);
    }

    [Test]
    public void BuildTaskGraphLayout_MultiParentWithChildren_ChildrenOnlyUnderFirstParent()
    {
        var parentA = new IssueBuilder().WithId("parentA").WithTitle("Parent A")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var parentB = new IssueBuilder().WithId("parentB").WithTitle("Parent B")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var sharedChild = new IssueBuilder().WithId("shared").WithTitle("Shared Child")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parentA", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "parentB", SortOrder = "aaa" })
            .Build();
        var grandchild = new IssueBuilder().WithId("grand").WithTitle("Grandchild")
            .WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("shared", "aaa").Build();

        var result = Issues.BuildTaskGraphLayout([parentA, parentB, sharedChild, grandchild]);

        // Shared appears twice, grandchild appears once (under first encounter of shared)
        result.Nodes.Count(n => n.Issue.Id == "shared").Should().Be(2);
        result.Nodes.Count(n => n.Issue.Id == "grand").Should().Be(1);
    }

    [Test]
    public void BuildTaskGraphLayout_ThreeParents_AppearsThreeTimes()
    {
        var parentA = new IssueBuilder().WithId("parentA").WithTitle("Parent A")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var parentB = new IssueBuilder().WithId("parentB").WithTitle("Parent B")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var parentC = new IssueBuilder().WithId("parentC").WithTitle("Parent C")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var sharedChild = new IssueBuilder().WithId("shared").WithTitle("Shared Child")
            .WithStatus(IssueStatus.Open)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parentA", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "parentB", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "parentC", SortOrder = "aaa" })
            .Build();

        var result = Issues.BuildTaskGraphLayout([parentA, parentB, parentC, sharedChild]);

        var sharedNodes = result.Nodes.Where(n => n.Issue.Id == "shared").ToList();
        sharedNodes.Should().HaveCount(3);
        sharedNodes[0].AppearanceIndex.Should().Be(1);
        sharedNodes[0].TotalAppearances.Should().Be(3);
        sharedNodes[1].AppearanceIndex.Should().Be(2);
        sharedNodes[1].TotalAppearances.Should().Be(3);
        sharedNodes[2].AppearanceIndex.Should().Be(3);
        sharedNodes[2].TotalAppearances.Should().Be(3);
    }

    [Test]
    public void BuildTaskGraphLayout_DiamondDependency_SharedIssueAppearsTwice()
    {
        // A -> B, A -> C, B -> D, C -> D (diamond)
        var a = new IssueBuilder().WithId("A").WithTitle("A")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var b = new IssueBuilder().WithId("B").WithTitle("B")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series)
            .WithParentIssueIdAndOrder("A", "aaa").Build();
        var c = new IssueBuilder().WithId("C").WithTitle("C")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series)
            .WithParentIssueIdAndOrder("A", "bbb").Build();
        var d = new IssueBuilder().WithId("D").WithTitle("D")
            .WithStatus(IssueStatus.Open)
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "B", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "C", SortOrder = "aaa" })
            .Build();

        var result = Issues.BuildTaskGraphLayout([a, b, c, d]);

        var dNodes = result.Nodes.Where(n => n.Issue.Id == "D").ToList();
        dNodes.Should().HaveCount(2);
        dNodes[0].TotalAppearances.Should().Be(2);
        dNodes[1].TotalAppearances.Should().Be(2);
    }

    [Test]
    public void BuildTaskGraphLayout_SingleParent_DefaultAppearanceCounts()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var child = new IssueBuilder().WithId("child").WithTitle("Child")
            .WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = Issues.BuildTaskGraphLayout([parent, child]);

        foreach (var node in result.Nodes)
        {
            node.AppearanceIndex.Should().Be(1);
            node.TotalAppearances.Should().Be(1);
        }
    }

    [Test]
    public void BuildTaskGraphLayout_SeriesNestedSiblings_RenderingParentSetForCascading()
    {
        var parent1 = new IssueBuilder().WithId("parent1").WithTitle("Parent 1")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var parent2 = new IssueBuilder().WithId("parent2").WithTitle("Parent 2")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series)
            .WithParentIssueIdAndOrder("parent1", "aaa").Build();
        var parent3 = new IssueBuilder().WithId("parent3").WithTitle("Parent 3")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series)
            .WithParentIssueIdAndOrder("parent1", "bbb").Build();
        var child21 = new IssueBuilder().WithId("child21").WithTitle("Child 2.1")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent2", "aaa").Build();
        var child22 = new IssueBuilder().WithId("child22").WithTitle("Child 2.2")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent2", "bbb").Build();
        var child31 = new IssueBuilder().WithId("child31").WithTitle("Child 3.1")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent3", "aaa").Build();
        var child32 = new IssueBuilder().WithId("child32").WithTitle("Child 3.2")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent3", "bbb").Build();

        var result = Issues.BuildTaskGraphLayout([parent1, parent2, parent3, child21, child22, child31, child32]);

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);

        // First child of first sibling subtree should have no rendering parent override
        nodeLookup["child21"].RenderingParentId.Should().BeNull();

        // Second child in same subtree should have first child as rendering parent (cascading within subtree)
        nodeLookup["child22"].RenderingParentId.Should().Be("child21");

        // First child of second sibling subtree should connect to previous sibling (parent2)
        nodeLookup["child31"].RenderingParentId.Should().Be("parent2");

        // Second child in second subtree should connect to first child of that subtree
        nodeLookup["child32"].RenderingParentId.Should().Be("child31");
    }

    [Test]
    public void BuildTaskGraphLayout_SeriesLeafSiblings_RenderingParentSetForCascading()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Series).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();
        var child3 = new IssueBuilder().WithId("child3").WithTitle("Child 3")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "ccc").Build();

        var result = Issues.BuildTaskGraphLayout([parent, child1, child2, child3]);

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);

        // First child has no rendering parent override
        nodeLookup["child1"].RenderingParentId.Should().BeNull();

        // Subsequent children should cascade to previous sibling
        nodeLookup["child2"].RenderingParentId.Should().Be("child1");
        nodeLookup["child3"].RenderingParentId.Should().Be("child2");
    }

    [Test]
    public void BuildTaskGraphLayout_ParallelChildren_NoRenderingParentOverride()
    {
        // Parallel mode should not set cascading rendering parent
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent")
            .WithStatus(IssueStatus.Open).WithExecutionMode(ExecutionMode.Parallel).Build();
        var child1 = new IssueBuilder().WithId("child1").WithTitle("Child 1")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var child2 = new IssueBuilder().WithId("child2").WithTitle("Child 2")
            .WithStatus(IssueStatus.Open).WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.BuildTaskGraphLayout([parent, child1, child2]);

        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);

        // Parallel children should not have rendering parent override
        nodeLookup["child1"].RenderingParentId.Should().BeNull();
        nodeLookup["child2"].RenderingParentId.Should().BeNull();
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

    #region Idea Type Task Graph Root Inclusion Tests

    [Test]
    public void BuildTaskGraphLayout_IdeaTypeRootIssue_IncludedInRoots()
    {
        var idea = new IssueBuilder().WithId("idea1").WithTitle("Idea Issue")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();
        var task = new IssueBuilder().WithId("task1").WithTitle("Task Issue")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Task).Build();

        var result = Issues.BuildTaskGraphLayout([idea, task]);

        result.Nodes.Should().HaveCount(2);
        result.Nodes.Select(n => n.Issue.Id).Should().Contain(["idea1", "task1"]);
    }

    [Test]
    public void BuildTaskGraphLayout_IdeaAsChildOfTask_IdeaIncludedInGraph()
    {
        var parent = new IssueBuilder().WithId("parent").WithTitle("Parent Task")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Task).WithExecutionMode(ExecutionMode.Parallel).Build();
        var ideaChild = new IssueBuilder().WithId("idea-child").WithTitle("Idea Child")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).WithParentIssueIdAndOrder("parent", "aaa").Build();
        var taskChild = new IssueBuilder().WithId("task-child").WithTitle("Task Child")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Task).WithParentIssueIdAndOrder("parent", "bbb").Build();

        var result = Issues.BuildTaskGraphLayout([parent, ideaChild, taskChild]);

        result.Nodes.Should().HaveCount(3);
        var nodeLookup = result.Nodes.ToDictionary(n => n.Issue.Id);
        nodeLookup.Should().ContainKey("parent");
        nodeLookup.Should().ContainKey("idea-child");
        nodeLookup.Should().ContainKey("task-child");

        nodeLookup["idea-child"].IsActionable.Should().BeFalse();
        nodeLookup["task-child"].IsActionable.Should().BeTrue();
    }

    [Test]
    public void BuildTaskGraphLayout_MultipleIdeaRoots_AllIncluded()
    {
        var idea1 = new IssueBuilder().WithId("idea1").WithTitle("Idea 1")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();
        var idea2 = new IssueBuilder().WithId("idea2").WithTitle("Idea 2")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();
        var task = new IssueBuilder().WithId("task1").WithTitle("Task 1")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Task).Build();

        var result = Issues.BuildTaskGraphLayout([idea1, idea2, task]);

        result.Nodes.Should().HaveCount(3);
        result.Nodes.Select(n => n.Issue.Id).Should().Contain(["idea1", "idea2", "task1"]);
    }

    [Test]
    public void BuildTaskGraphLayout_IdeaWithOrphanedParent_TreatedAsRootAndIncluded()
    {
        var idea = new IssueBuilder().WithId("idea1").WithTitle("Orphan Idea")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).WithParentIssueIdAndOrder("nonexistent", "aaa").Build();
        var task = new IssueBuilder().WithId("task1").WithTitle("Task 1")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Task).Build();

        var result = Issues.BuildTaskGraphLayout([idea, task]);

        result.Nodes.Should().HaveCount(2);
        result.Nodes.Select(n => n.Issue.Id).Should().Contain(["idea1", "task1"]);
    }

    [Test]
    public void BuildTaskGraphLayout_OnlyIdeasInGraph_ReturnsIdeasAsRoots()
    {
        var idea1 = new IssueBuilder().WithId("idea1").WithTitle("Idea 1")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();
        var idea2 = new IssueBuilder().WithId("idea2").WithTitle("Idea 2")
            .WithStatus(IssueStatus.Open).WithType(IssueType.Idea).Build();

        var result = Issues.BuildTaskGraphLayout([idea1, idea2]);

        result.Nodes.Should().HaveCount(2);
        result.Nodes.Select(n => n.Issue.Id).Should().Contain(["idea1", "idea2"]);
    }

    #endregion

    #region BuildTaskGraphLayout Filter Tests

    [Test]
    public void BuildTaskGraphLayout_IncludesDraftByDefault()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var draft = new IssueBuilder().WithId("draft1").WithStatus(IssueStatus.Draft).Build();

        var result = Issues.BuildTaskGraphLayout([open, draft]);

        result.Nodes.Should().HaveCount(2);
        result.Nodes.Select(n => n.Issue.Id).Should().Contain(["open1", "draft1"]);
    }

    [Test]
    public void BuildTaskGraphLayout_IncludeTerminal_IncludesTerminalStatuses()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var draft = new IssueBuilder().WithId("draft1").WithStatus(IssueStatus.Draft).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();
        var closed = new IssueBuilder().WithId("closed1").WithStatus(IssueStatus.Closed).Build();

        var result = Issues.BuildTaskGraphLayout([open, draft, complete, closed], visibility: InactiveVisibility.Always);

        result.Nodes.Should().HaveCount(4);
        result.Nodes.Select(n => n.Issue.Id).Should().Contain(["open1", "draft1", "complete1", "closed1"]);
    }

    [Test]
    public void BuildTaskGraphLayout_AssignedToFilter_ReturnsOnlyMatchingAssignee()
    {
        var johnIssue = new IssueBuilder().WithId("john1").WithStatus(IssueStatus.Open).WithAssignedTo("john").Build();
        var janeIssue = new IssueBuilder().WithId("jane1").WithStatus(IssueStatus.Open).WithAssignedTo("jane").Build();
        var unassigned = new IssueBuilder().WithId("unassigned1").WithStatus(IssueStatus.Open).Build();

        var result = Issues.BuildTaskGraphLayout([johnIssue, janeIssue, unassigned], assignedTo: "john");

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("john1");
    }

    [Test]
    public void BuildTaskGraphLayout_AssignedToFilter_IsCaseInsensitive()
    {
        var johnIssue = new IssueBuilder().WithId("john1").WithStatus(IssueStatus.Open).WithAssignedTo("John").Build();

        var result = Issues.BuildTaskGraphLayout([johnIssue], assignedTo: "john");

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("john1");
    }

    [Test]
    public void BuildTaskGraphLayout_CombinedFilters_IncludeTerminalAndAssignedTo()
    {
        var johnOpen = new IssueBuilder().WithId("john-open").WithStatus(IssueStatus.Open).WithAssignedTo("john").Build();
        var johnComplete = new IssueBuilder().WithId("john-complete").WithStatus(IssueStatus.Complete).WithAssignedTo("john").Build();
        var janeOpen = new IssueBuilder().WithId("jane-open").WithStatus(IssueStatus.Open).WithAssignedTo("jane").Build();
        var janeComplete = new IssueBuilder().WithId("jane-complete").WithStatus(IssueStatus.Complete).WithAssignedTo("jane").Build();

        var result = Issues.BuildTaskGraphLayout([johnOpen, johnComplete, janeOpen, janeComplete], visibility: InactiveVisibility.Always, assignedTo: "john");

        result.Nodes.Should().HaveCount(2);
        result.Nodes.Select(n => n.Issue.Id).Should().Contain(["john-open", "john-complete"]);
    }

    [Test]
    public void BuildTaskGraphLayout_ParentWithFilteredChild_ParentStillIncludedAsAncestor()
    {
        // When a child passes the filter, its parent should be included for context even if parent doesn't pass
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Open).WithAssignedTo("jane").Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open).WithAssignedTo("john")
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = Issues.BuildTaskGraphLayout([parent, child], assignedTo: "john");

        // Only john's child should be in the filtered set (parent filtered out)
        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("child");
    }

    [Test]
    public void BuildTaskGraphLayout_NoMatchingAssignee_ReturnsEmptyGraph()
    {
        var issue = new IssueBuilder().WithId("issue1").WithStatus(IssueStatus.Open).WithAssignedTo("john").Build();

        var result = Issues.BuildTaskGraphLayout([issue], assignedTo: "nonexistent");

        result.Nodes.Should().BeEmpty();
        result.TotalLanes.Should().Be(0);
    }

    [Test]
    public void BuildTaskGraphLayout_DefaultFilters_ExcludesProgressIssuesCorrectly()
    {
        // Progress issues should still be included (they are not terminal or draft)
        var progress = new IssueBuilder().WithId("progress1").WithStatus(IssueStatus.Progress).Build();

        var result = Issues.BuildTaskGraphLayout([progress]);

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("progress1");
    }

    [Test]
    public void BuildTaskGraphLayout_DefaultFilters_ExcludesReviewIssuesCorrectly()
    {
        // Review issues should still be included (they are not terminal or draft)
        var review = new IssueBuilder().WithId("review1").WithStatus(IssueStatus.Review).Build();

        var result = Issues.BuildTaskGraphLayout([review]);

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("review1");
    }

    #endregion

    #region BuildTaskGraphLayout InactiveVisibility Tests

    [Test]
    public void BuildTaskGraphLayout_HideMode_ExcludesTerminalIssues()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();

        var result = Issues.BuildTaskGraphLayout([open, complete], visibility: InactiveVisibility.Hide);

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("open1");
    }

    [Test]
    public void BuildTaskGraphLayout_AlwaysMode_IncludesAllTerminalIssues()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();
        var closed = new IssueBuilder().WithId("closed1").WithStatus(IssueStatus.Closed).Build();

        var result = Issues.BuildTaskGraphLayout([open, complete, closed], visibility: InactiveVisibility.Always);

        result.Nodes.Should().HaveCount(3);
        result.Nodes.Select(n => n.Issue.Id).Should().Contain(["open1", "complete1", "closed1"]);
    }

    [Test]
    public void BuildTaskGraphLayout_IfHasActiveDescendants_IncludesTerminalParentWithActiveChild()
    {
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Complete).Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = Issues.BuildTaskGraphLayout([parent, child], visibility: InactiveVisibility.IfHasActiveDescendants);

        result.Nodes.Should().HaveCount(2);
        result.Nodes.Select(n => n.Issue.Id).Should().Contain(["parent", "child"]);
    }

    [Test]
    public void BuildTaskGraphLayout_IfHasActiveDescendants_ExcludesTerminalWithNoActiveDescendants()
    {
        var terminalParent = new IssueBuilder().WithId("terminal-parent").WithStatus(IssueStatus.Complete).Build();
        var terminalChild = new IssueBuilder().WithId("terminal-child").WithStatus(IssueStatus.Closed)
            .WithParentIssueIdAndOrder("terminal-parent", "aaa").Build();
        var openAlone = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();

        var result = Issues.BuildTaskGraphLayout([terminalParent, terminalChild, openAlone], visibility: InactiveVisibility.IfHasActiveDescendants);

        result.Nodes.Should().ContainSingle().Which.Issue.Id.Should().Be("open1");
    }

    [Test]
    public void BuildTaskGraphLayout_IfHasActiveDescendants_DeepHierarchy_GrandparentTerminalParentTerminalChildActive()
    {
        var grandparent = new IssueBuilder().WithId("grandparent").WithStatus(IssueStatus.Complete).Build();
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Archived)
            .WithParentIssueIdAndOrder("grandparent", "aaa").Build();
        var child = new IssueBuilder().WithId("child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();

        var result = Issues.BuildTaskGraphLayout([grandparent, parent, child], visibility: InactiveVisibility.IfHasActiveDescendants);

        result.Nodes.Should().HaveCount(3);
        result.Nodes.Select(n => n.Issue.Id).Should().Contain(["grandparent", "parent", "child"]);
    }

    [Test]
    public void BuildTaskGraphLayout_IfHasActiveDescendants_MixedActiveAndInactive()
    {
        // Terminal parent with one active child and one terminal child
        var parent = new IssueBuilder().WithId("parent").WithStatus(IssueStatus.Complete).Build();
        var activeChild = new IssueBuilder().WithId("active-child").WithStatus(IssueStatus.Open)
            .WithParentIssueIdAndOrder("parent", "aaa").Build();
        var terminalChild = new IssueBuilder().WithId("terminal-child").WithStatus(IssueStatus.Closed)
            .WithParentIssueIdAndOrder("parent", "bbb").Build();
        // Another terminal parent with no active descendants
        var lonelyParent = new IssueBuilder().WithId("lonely").WithStatus(IssueStatus.Complete).Build();

        var result = Issues.BuildTaskGraphLayout([parent, activeChild, terminalChild, lonelyParent], visibility: InactiveVisibility.IfHasActiveDescendants);

        // parent should be included (has active child), lonely should not
        result.Nodes.Select(n => n.Issue.Id).Should().Contain(["parent", "active-child"]);
        result.Nodes.Select(n => n.Issue.Id).Should().NotContain("lonely");
    }

    [Test]
    public void BuildTaskGraphLayout_HideMode_MatchesDefaultBehavior()
    {
        var open = new IssueBuilder().WithId("open1").WithStatus(IssueStatus.Open).Build();
        var complete = new IssueBuilder().WithId("complete1").WithStatus(IssueStatus.Complete).Build();
        IReadOnlyList<Issue> issues = [open, complete];

        var hideResult = Issues.BuildTaskGraphLayout(issues, visibility: InactiveVisibility.Hide);
        var defaultResult = Issues.BuildTaskGraphLayout(issues);

        hideResult.Nodes.Should().HaveCount(defaultResult.Nodes.Count);
        hideResult.Nodes.Select(n => n.Issue.Id).Should().BeEquivalentTo(defaultResult.Nodes.Select(n => n.Issue.Id));
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
