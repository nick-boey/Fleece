using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Search;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.FunctionalCore;

[TestFixture]
public class SearchTests
{
    private static Issue CreateIssue(
        string id = "abc123",
        string title = "Test Issue",
        string? description = null,
        IssueStatus status = IssueStatus.Open,
        IssueType type = IssueType.Task,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null)
    {
        return new Issue
        {
            Id = id,
            Title = title,
            Description = description,
            Status = status,
            Type = type,
            Priority = priority,
            AssignedTo = assignedTo,
            Tags = tags ?? [],
            LinkedPR = linkedPr,
            LastUpdate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            ParentIssues = [],
            LinkedIssues = []
        };
    }

    #region Basic Filtering

    [Test]
    public void SearchAsync_StatusFilter_FiltersCorrectly()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", status: IssueStatus.Open),
            CreateIssue(id: "2", status: IssueStatus.Progress),
            CreateIssue(id: "3", status: IssueStatus.Open)
        };

        var query = SearchOps.ParseQuery("status:open");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.Status == IssueStatus.Open);
    }

    [Test]
    public void SearchAsync_TypeFilter_FiltersCorrectly()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", type: IssueType.Bug),
            CreateIssue(id: "2", type: IssueType.Feature),
            CreateIssue(id: "3", type: IssueType.Bug)
        };

        var query = SearchOps.ParseQuery("type:bug");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.Type == IssueType.Bug);
    }

    [Test]
    public void SearchAsync_PriorityFilter_FiltersCorrectly()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", priority: 1),
            CreateIssue(id: "2", priority: 2),
            CreateIssue(id: "3", priority: 1)
        };

        var query = SearchOps.ParseQuery("priority:1");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.Priority == 1);
    }

    [Test]
    public void SearchAsync_AssignedFilter_FiltersCorrectly()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", assignedTo: "john"),
            CreateIssue(id: "2", assignedTo: "jane"),
            CreateIssue(id: "3", assignedTo: "john")
        };

        var query = SearchOps.ParseQuery("assigned:john");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.AssignedTo == "john");
    }

    [Test]
    public void SearchAsync_TagFilter_FiltersCorrectly()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", tags: ["backend", "api"]),
            CreateIssue(id: "2", tags: ["frontend"]),
            CreateIssue(id: "3", tags: ["backend", "urgent"])
        };

        var query = SearchOps.ParseQuery("tag:backend");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.Tags.Contains("backend"));
    }

    [Test]
    public void SearchAsync_LinkedPrFilter_FiltersCorrectly()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", linkedPr: 123),
            CreateIssue(id: "2", linkedPr: 456),
            CreateIssue(id: "3", linkedPr: 123)
        };

        var query = SearchOps.ParseQuery("pr:123");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.LinkedPR == 123);
    }

    #endregion

    #region Negation

    [Test]
    public void SearchAsync_NegatedStatusFilter_ExcludesStatus()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", status: IssueStatus.Open),
            CreateIssue(id: "2", status: IssueStatus.Progress),
            CreateIssue(id: "3", status: IssueStatus.Review)
        };

        var query = SearchOps.ParseQuery("-status:open");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
        result.Should().NotContain(i => i.Status == IssueStatus.Open);
    }

    [Test]
    public void SearchAsync_NegatedMultiValue_ExcludesAllValues()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", type: IssueType.Bug),
            CreateIssue(id: "2", type: IssueType.Feature),
            CreateIssue(id: "3", type: IssueType.Task),
            CreateIssue(id: "4", type: IssueType.Chore)
        };

        var query = SearchOps.ParseQuery("-type:bug, chore;");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
        result.Should().NotContain(i => i.Type == IssueType.Bug);
        result.Should().NotContain(i => i.Type == IssueType.Chore);
    }

    #endregion

    #region Multi-Value (OR within filter)

    [Test]
    public void SearchAsync_MultiValueStatus_MatchesAny()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", status: IssueStatus.Open),
            CreateIssue(id: "2", status: IssueStatus.Progress),
            CreateIssue(id: "3", status: IssueStatus.Review)
        };

        var query = SearchOps.ParseQuery("status:open, progress;");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
        result.Should().Contain(i => i.Status == IssueStatus.Open);
        result.Should().Contain(i => i.Status == IssueStatus.Progress);
    }

    [Test]
    public void SearchAsync_MultiValueType_MatchesAny()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", type: IssueType.Bug),
            CreateIssue(id: "2", type: IssueType.Feature),
            CreateIssue(id: "3", type: IssueType.Task)
        };

        var query = SearchOps.ParseQuery("type:bug, feature;");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
        result.Should().Contain(i => i.Type == IssueType.Bug);
        result.Should().Contain(i => i.Type == IssueType.Feature);
    }

    #endregion

    #region Full-Text Search

    [Test]
    public void SearchAsync_TextToken_SearchesTitle()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", title: "Fix login bug"),
            CreateIssue(id: "2", title: "Add feature"),
            CreateIssue(id: "3", title: "Login page update")
        };

        var query = SearchOps.ParseQuery("login");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
    }

    [Test]
    public void SearchAsync_TextToken_SearchesDescription()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", title: "Issue 1", description: "Fix the authentication flow"),
            CreateIssue(id: "2", title: "Issue 2", description: "Update database"),
            CreateIssue(id: "3", title: "Issue 3", description: "Authentication service")
        };

        var query = SearchOps.ParseQuery("authentication");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
    }

    [Test]
    public void SearchAsync_TextToken_SearchesTags()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", title: "Issue 1", tags: ["backend", "api"]),
            CreateIssue(id: "2", title: "Issue 2", tags: ["frontend"]),
            CreateIssue(id: "3", title: "Issue 3", tags: ["api-gateway"])
        };

        var query = SearchOps.ParseQuery("api");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(2);
    }

    [Test]
    public void SearchAsync_MultipleTextTokens_UsesAndLogic()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", title: "Fix login authentication"),
            CreateIssue(id: "2", title: "Fix login display"),
            CreateIssue(id: "3", title: "Update authentication")
        };

        var query = SearchOps.ParseQuery("login authentication");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("1");
    }

    [Test]
    public void SearchAsync_TextToken_CaseInsensitive()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", title: "Fix LOGIN bug"),
            CreateIssue(id: "2", title: "Add feature")
        };

        var query = SearchOps.ParseQuery("login");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(1);
    }

    #endregion

    #region Combined Filters (AND logic)

    [Test]
    public void SearchAsync_MultipleFilters_UsesAndLogic()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", status: IssueStatus.Open, type: IssueType.Bug),
            CreateIssue(id: "2", status: IssueStatus.Open, type: IssueType.Feature),
            CreateIssue(id: "3", status: IssueStatus.Progress, type: IssueType.Bug)
        };

        var query = SearchOps.ParseQuery("status:open type:bug");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("1");
    }

    [Test]
    public void SearchAsync_FilterAndText_CombinesWithAnd()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", status: IssueStatus.Open, title: "Login bug"),
            CreateIssue(id: "2", status: IssueStatus.Open, title: "Database issue"),
            CreateIssue(id: "3", status: IssueStatus.Progress, title: "Login update")
        };

        var query = SearchOps.ParseQuery("status:open login");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("1");
    }

    #endregion

    #region CLI Filter Precedence

    [Test]
    public void SearchWithFiltersAsync_CliOverridesQueryForSameField()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", status: IssueStatus.Open),
            CreateIssue(id: "2", status: IssueStatus.Progress)
        };

        // Query says "status:progress" but CLI says "open"
        var query = SearchOps.ParseQuery("status:progress");
        var result = SearchOps.SearchWithFilters(issues, query, status: IssueStatus.Open);

        result.Should().HaveCount(1);
        result[0].Status.Should().Be(IssueStatus.Open);
    }

    [Test]
    public void SearchWithFiltersAsync_CliAndQueryCombineForDifferentFields()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", status: IssueStatus.Open, type: IssueType.Bug),
            CreateIssue(id: "2", status: IssueStatus.Open, type: IssueType.Feature),
            CreateIssue(id: "3", status: IssueStatus.Progress, type: IssueType.Bug)
        };

        // Query says "type:bug" and CLI says "status:open"
        var query = SearchOps.ParseQuery("type:bug");
        var result = SearchOps.SearchWithFilters(issues, query, status: IssueStatus.Open);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("1");
    }

    #endregion

    #region Terminal Status Handling

    [Test]
    public void SearchAsync_ExcludesTerminalByDefault()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", status: IssueStatus.Open),
            CreateIssue(id: "2", status: IssueStatus.Complete),
            CreateIssue(id: "3", status: IssueStatus.Archived)
        };

        var query = SearchOps.ParseQuery("");
        var result = SearchOps.Search(issues, query, includeTerminal: false);

        result.Should().HaveCount(1);
        result[0].Status.Should().Be(IssueStatus.Open);
    }

    [Test]
    public void SearchAsync_IncludesTerminalWhenRequested()
    {
        var issues = new List<Issue>
        {
            CreateIssue(id: "1", status: IssueStatus.Open),
            CreateIssue(id: "2", status: IssueStatus.Complete),
            CreateIssue(id: "3", status: IssueStatus.Archived)
        };

        var query = SearchOps.ParseQuery("");
        var result = SearchOps.Search(issues, query, includeTerminal: true);

        result.Should().HaveCount(3);
    }

    #endregion

    #region Context Search

    [Test]
    public void SearchWithContextAsync_ReturnsMatchedAndContextIssues()
    {
        var parentRef = new ParentIssueRef { ParentIssue = "parent1", SortOrder = "aaa" };
        var issues = new List<Issue>
        {
            new Issue
            {
                Id = "parent1",
                Title = "Parent Issue",
                Status = IssueStatus.Open,
                Type = IssueType.Feature,
                LastUpdate = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                ParentIssues = [],
                LinkedIssues = [],
                Tags = []
            },
            new Issue
            {
                Id = "child1",
                Title = "Child with login",
                Status = IssueStatus.Open,
                Type = IssueType.Task,
                LastUpdate = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                ParentIssues = [parentRef],
                LinkedIssues = [],
                Tags = []
            }
        };

        var query = SearchOps.ParseQuery("login");
        var result = SearchOps.SearchWithContext(issues, query);

        result.MatchedIssues.Should().HaveCount(1);
        result.MatchedIssues[0].Id.Should().Be("child1");
        result.MatchedIds.Should().Contain("child1");
        result.ContextIssues.Should().HaveCount(1);
        result.ContextIssues[0].Id.Should().Be("parent1");
    }

    #endregion

    #region Matches Method

    [Test]
    public void Matches_EmptyQuery_ReturnsTrue()
    {
        var issue = CreateIssue();
        var query = SearchQuery.Empty;

        var result = SearchOps.Matches(issue, query);

        result.Should().BeTrue();
    }

    [Test]
    public void Matches_MatchingFilter_ReturnsTrue()
    {
        var issue = CreateIssue(status: IssueStatus.Open);
        var query = SearchOps.ParseQuery("status:open");

        var result = SearchOps.Matches(issue, query);

        result.Should().BeTrue();
    }

    [Test]
    public void Matches_NonMatchingFilter_ReturnsFalse()
    {
        var issue = CreateIssue(status: IssueStatus.Progress);
        var query = SearchOps.ParseQuery("status:open");

        var result = SearchOps.Matches(issue, query);

        result.Should().BeFalse();
    }

    #endregion
}
