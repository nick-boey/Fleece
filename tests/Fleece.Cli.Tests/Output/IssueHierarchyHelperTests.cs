using Fleece.Cli.Output;
using Fleece.Core.Models;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Cli.Tests.Output;

[TestFixture]
public class IssueHierarchyHelperTests
{
    [Test]
    public void BuildShowContext_NoParentsNoChildren_ReturnsEmptyContext()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Standalone issue")
            .Build();

        var allIssues = new List<Issue> { issue };

        var result = IssueHierarchyHelper.BuildShowContext(issue, allIssues);

        result.Issue.Id.Should().Be("abc123");
        result.Parents.Should().BeEmpty();
        result.Children.Should().BeEmpty();
        result.ExecutionMode.Should().Be(ExecutionMode.Series);
    }

    [Test]
    public void BuildShowContext_SeriesParent_ReturnsPositionAndSiblings()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First Child")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Second Child")
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var child3 = new IssueBuilder()
            .WithId("child3")
            .WithTitle("Third Child")
            .WithParentIssueIdAndOrder("parent1", "aac")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2, child3 };

        var result = IssueHierarchyHelper.BuildShowContext(child2, allIssues);

        result.Parents.Should().HaveCount(1);
        var parentContext = result.Parents[0];
        parentContext.Parent.Id.Should().Be("parent1");
        parentContext.ExecutionMode.Should().Be(ExecutionMode.Series);
        parentContext.Position.Should().Be(2);
        parentContext.TotalSiblings.Should().Be(3);
        parentContext.PreviousSibling.Should().NotBeNull();
        parentContext.PreviousSibling!.Id.Should().Be("child1");
        parentContext.NextSibling.Should().NotBeNull();
        parentContext.NextSibling!.Id.Should().Be("child3");
    }

    [Test]
    public void BuildShowContext_ParallelParent_OmitsPositionAndSiblings()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Parallel)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First Child")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Second Child")
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2 };

        var result = IssueHierarchyHelper.BuildShowContext(child1, allIssues);

        result.Parents.Should().HaveCount(1);
        var parentContext = result.Parents[0];
        parentContext.Parent.Id.Should().Be("parent1");
        parentContext.ExecutionMode.Should().Be(ExecutionMode.Parallel);
        parentContext.Position.Should().BeNull();
        parentContext.TotalSiblings.Should().BeNull();
        parentContext.PreviousSibling.Should().BeNull();
        parentContext.NextSibling.Should().BeNull();
    }

    [Test]
    public void BuildShowContext_FirstChild_NoPreviousSibling()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First Child")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Second Child")
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2 };

        var result = IssueHierarchyHelper.BuildShowContext(child1, allIssues);

        var parentContext = result.Parents[0];
        parentContext.Position.Should().Be(1);
        parentContext.TotalSiblings.Should().Be(2);
        parentContext.PreviousSibling.Should().BeNull();
        parentContext.NextSibling.Should().NotBeNull();
        parentContext.NextSibling!.Id.Should().Be("child2");
    }

    [Test]
    public void BuildShowContext_LastChild_NoNextSibling()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First Child")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Last Child")
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2 };

        var result = IssueHierarchyHelper.BuildShowContext(child2, allIssues);

        var parentContext = result.Parents[0];
        parentContext.Position.Should().Be(2);
        parentContext.TotalSiblings.Should().Be(2);
        parentContext.PreviousSibling.Should().NotBeNull();
        parentContext.PreviousSibling!.Id.Should().Be("child1");
        parentContext.NextSibling.Should().BeNull();
    }

    [Test]
    public void BuildShowContext_MultipleParents_ReturnsContextForEach()
    {
        var parent1 = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent One")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var parent2 = new IssueBuilder()
            .WithId("parent2")
            .WithTitle("Parent Two")
            .WithExecutionMode(ExecutionMode.Parallel)
            .Build();

        var child = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Shared Child")
            .WithParentIssues(
                new ParentIssueRef { ParentIssue = "parent1", SortOrder = "aaa" },
                new ParentIssueRef { ParentIssue = "parent2", SortOrder = "aaa" })
            .Build();

        var allIssues = new List<Issue> { parent1, parent2, child };

        var result = IssueHierarchyHelper.BuildShowContext(child, allIssues);

        result.Parents.Should().HaveCount(2);
        result.Parents[0].Parent.Id.Should().Be("parent1");
        result.Parents[0].ExecutionMode.Should().Be(ExecutionMode.Series);
        result.Parents[1].Parent.Id.Should().Be("parent2");
        result.Parents[1].ExecutionMode.Should().Be(ExecutionMode.Parallel);
    }

    [Test]
    public void BuildShowContext_MissingParent_SkipsIt()
    {
        var child = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Orphan Child")
            .WithParentIssueIdAndOrder("missing_parent", "aaa")
            .Build();

        var allIssues = new List<Issue> { child };

        var result = IssueHierarchyHelper.BuildShowContext(child, allIssues);

        result.Parents.Should().BeEmpty();
    }

    [Test]
    public void BuildShowContext_WithChildren_ReturnsChildList()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First Child")
            .WithType(IssueType.Bug)
            .WithStatus(IssueStatus.Open)
            .WithPriority(1)
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Second Child")
            .WithType(IssueType.Task)
            .WithStatus(IssueStatus.Progress)
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2 };

        var result = IssueHierarchyHelper.BuildShowContext(parent, allIssues);

        result.Children.Should().HaveCount(2);
        // child2 has sort order "aaa" which comes before child1's "aab"
        result.Children[0].Id.Should().Be("child2");
        result.Children[1].Id.Should().Be("child1");
    }

    [Test]
    public void BuildShowContext_ChildrenSortedByPriorityWhenSameSortOrder()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Low priority")
            .WithPriority(3)
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("High priority")
            .WithPriority(1)
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2 };

        var result = IssueHierarchyHelper.BuildShowContext(parent, allIssues);

        result.Children.Should().HaveCount(2);
        result.Children[0].Id.Should().Be("child2"); // priority 1 first
        result.Children[1].Id.Should().Be("child1"); // priority 3 second
    }

    [Test]
    public void BuildShowContext_ChildrenSortedByTitleAsTiebreaker()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Zulu task")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Alpha task")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2 };

        var result = IssueHierarchyHelper.BuildShowContext(parent, allIssues);

        result.Children.Should().HaveCount(2);
        result.Children[0].Id.Should().Be("child2"); // "Alpha task" first
        result.Children[1].Id.Should().Be("child1"); // "Zulu task" second
    }

    [Test]
    public void BuildShowContext_PreservesIssueExecutionMode()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Parallel parent")
            .WithExecutionMode(ExecutionMode.Parallel)
            .Build();

        var allIssues = new List<Issue> { issue };

        var result = IssueHierarchyHelper.BuildShowContext(issue, allIssues);

        result.ExecutionMode.Should().Be(ExecutionMode.Parallel);
    }

    [Test]
    public void BuildShowContext_IssueDtoFieldsPopulated()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Test issue")
            .WithStatus(IssueStatus.Progress)
            .WithType(IssueType.Feature)
            .WithPriority(2)
            .Build();

        var allIssues = new List<Issue> { issue };

        var result = IssueHierarchyHelper.BuildShowContext(issue, allIssues);

        result.Issue.Id.Should().Be("abc123");
        result.Issue.Title.Should().Be("Test issue");
        result.Issue.Status.Should().Be(IssueStatus.Progress);
        result.Issue.Type.Should().Be(IssueType.Feature);
        result.Issue.Priority.Should().Be(2);
    }
}
