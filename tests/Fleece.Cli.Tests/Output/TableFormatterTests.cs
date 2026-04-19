using Fleece.Cli.Output;
using Fleece.Core.Models;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;
using Spectre.Console.Testing;

namespace Fleece.Cli.Tests.Output;

[TestFixture]
public class TableFormatterTests
{
    private TestConsole _console = null!;

    [SetUp]
    public void SetUp()
    {
        _console = new TestConsole();
    }

    [TearDown]
    public void TearDown()
    {
        _console.Dispose();
    }

    [Test]
    public void RenderIssue_WithSeriesParent_ShowsPositionAndSiblings()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First Child")
            .WithStatus(IssueStatus.Complete)
            .WithType(IssueType.Task)
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Second Child")
            .WithStatus(IssueStatus.Progress)
            .WithType(IssueType.Bug)
            .WithPriority(1)
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var child3 = new IssueBuilder()
            .WithId("child3")
            .WithTitle("Third Child")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Feature)
            .WithParentIssueIdAndOrder("parent1", "aac")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2, child3 };
        var showContext = IssueHierarchyHelper.BuildShowContext(child2, allIssues);

        TableFormatter.RenderIssue(_console, child2, showContext);

        var output = _console.Output;

        output.Should().Contain("Parent Issues");
        output.Should().Contain("parent1");
        output.Should().Contain("Parent Task");
        output.Should().Contain("series");
        output.Should().Contain("2 of 3");
        output.Should().Contain("Previous");
        output.Should().Contain("child1");
        output.Should().Contain("First Child");
        output.Should().Contain("Next");
        output.Should().Contain("child3");
        output.Should().Contain("Third Child");
    }

    [Test]
    public void RenderIssue_WithParallelParent_ShowsExecutionModeWithoutPosition()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parallel Parent")
            .WithExecutionMode(ExecutionMode.Parallel)
            .Build();

        var child = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Task")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var allIssues = new List<Issue> { parent, child };
        var showContext = IssueHierarchyHelper.BuildShowContext(child, allIssues);

        TableFormatter.RenderIssue(_console, child, showContext);

        var output = _console.Output;

        output.Should().Contain("Parent Issues");
        output.Should().Contain("parent1");
        output.Should().Contain("parallel");

        output.Should().NotContain("Position");
        output.Should().NotContain("Previous");
        output.Should().NotContain("Next");
    }

    [Test]
    public void RenderIssue_WithChildren_ShowsChildList()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First Child")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Second Child")
            .WithStatus(IssueStatus.Progress)
            .WithType(IssueType.Bug)
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2 };
        var showContext = IssueHierarchyHelper.BuildShowContext(parent, allIssues);

        TableFormatter.RenderIssue(_console, parent, showContext);

        var output = _console.Output;

        output.Should().Contain("Children");
        output.Should().Contain("series");
        output.Should().Contain("child1");
        output.Should().Contain("First Child");
        output.Should().Contain("child2");
        output.Should().Contain("Second Child");
    }

    [Test]
    public void RenderIssue_SeriesChildren_ShowsNumberedList()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Second")
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2 };
        var showContext = IssueHierarchyHelper.BuildShowContext(parent, allIssues);

        TableFormatter.RenderIssue(_console, parent, showContext);

        var output = _console.Output;

        output.Should().Contain("1.");
        output.Should().Contain("2.");
    }

    [Test]
    public void RenderIssue_NoContext_DoesNotShowHierarchySections()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Standalone issue")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .Build();

        TableFormatter.RenderIssue(_console, issue);

        var output = _console.Output;

        output.Should().Contain("abc123");
        output.Should().Contain("Standalone issue");
        output.Should().NotContain("Children");
        output.Should().NotContain("Parent Issues");
    }

    [Test]
    public void RenderIssue_FirstChildInSeries_NoPreviousSibling()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Second")
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2 };
        var showContext = IssueHierarchyHelper.BuildShowContext(child1, allIssues);

        TableFormatter.RenderIssue(_console, child1, showContext);

        var output = _console.Output;

        output.Should().Contain("1 of 2");
        output.Should().NotContain("Previous");
        output.Should().Contain("Next");
        output.Should().Contain("child2");
    }

    [Test]
    public void RenderIssue_LastChildInSeries_NoNextSibling()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child1 = new IssueBuilder()
            .WithId("child1")
            .WithTitle("First")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var child2 = new IssueBuilder()
            .WithId("child2")
            .WithTitle("Last")
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var allIssues = new List<Issue> { parent, child1, child2 };
        var showContext = IssueHierarchyHelper.BuildShowContext(child2, allIssues);

        TableFormatter.RenderIssue(_console, child2, showContext);

        var output = _console.Output;

        output.Should().Contain("2 of 2");
        output.Should().Contain("Previous");
        output.Should().Contain("child1");
        output.Should().NotContain("Next");
    }
}
