using Fleece.Cli.Output;
using Fleece.Core.Models;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;
using Spectre.Console;

namespace Fleece.Cli.Tests.Output;

[TestFixture]
public class TableFormatterTests
{
    private StringWriter _consoleOutput = null!;
    private TextWriter _originalConsole = null!;
    private IAnsiConsole _originalAnsiConsole = null!;

    [SetUp]
    public void SetUp()
    {
        _originalConsole = Console.Out;
        _originalAnsiConsole = AnsiConsole.Console;
        _consoleOutput = new StringWriter();
        Console.SetOut(_consoleOutput);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(_consoleOutput)
        });
    }

    [TearDown]
    public void TearDown()
    {
        AnsiConsole.Console = _originalAnsiConsole;
        Console.SetOut(_originalConsole);
        _consoleOutput.Dispose();
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

        TableFormatter.RenderIssue(child2, showContext);

        var output = _consoleOutput.ToString();

        // Should show parent issue section
        output.Should().Contain("Parent Issues");
        output.Should().Contain("parent1");
        output.Should().Contain("Parent Task");

        // Should show execution mode
        output.Should().Contain("series");

        // Should show position (2 of 3)
        output.Should().Contain("2 of 3");

        // Should show previous sibling
        output.Should().Contain("Previous");
        output.Should().Contain("child1");
        output.Should().Contain("First Child");

        // Should show next sibling
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

        TableFormatter.RenderIssue(child, showContext);

        var output = _consoleOutput.ToString();

        output.Should().Contain("Parent Issues");
        output.Should().Contain("parent1");
        output.Should().Contain("parallel");

        // Should NOT show position or previous/next for parallel mode
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

        TableFormatter.RenderIssue(parent, showContext);

        var output = _consoleOutput.ToString();

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

        TableFormatter.RenderIssue(parent, showContext);

        var output = _consoleOutput.ToString();

        // Series mode should use numbered prefixes
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

        TableFormatter.RenderIssue(issue);

        var output = _consoleOutput.ToString();

        output.Should().Contain("abc123");
        output.Should().Contain("Standalone issue");
        output.Should().NotContain("Children");
        // "Parent Issues" label should not appear if no parents and no context
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

        TableFormatter.RenderIssue(child1, showContext);

        var output = _consoleOutput.ToString();

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

        TableFormatter.RenderIssue(child2, showContext);

        var output = _consoleOutput.ToString();

        output.Should().Contain("2 of 2");
        output.Should().Contain("Previous");
        output.Should().Contain("child1");
        output.Should().NotContain("Next");
    }
}
