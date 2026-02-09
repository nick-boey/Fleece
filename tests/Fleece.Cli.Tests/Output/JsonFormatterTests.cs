using System.Text.Json;
using Fleece.Cli.Output;
using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Cli.Tests.Output;

[TestFixture]
public class JsonFormatterTests
{
    private StringWriter _consoleOutput = null!;
    private TextWriter _originalConsole = null!;

    [SetUp]
    public void SetUp()
    {
        _originalConsole = Console.Out;
        _consoleOutput = new StringWriter();
        Console.SetOut(_consoleOutput);
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetOut(_originalConsole);
        _consoleOutput.Dispose();
    }

    [Test]
    public void RenderIssueShow_WithSeriesParent_IncludesPositionAndSiblings()
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

        JsonFormatter.RenderIssueShow(showContext);

        var json = _consoleOutput.ToString().Trim();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify issue fields
        root.GetProperty("issue").GetProperty("id").GetString().Should().Be("child2");
        root.GetProperty("issue").GetProperty("title").GetString().Should().Be("Second Child");

        // Verify parent context
        var parents = root.GetProperty("parents");
        parents.GetArrayLength().Should().Be(1);
        var parentCtx = parents[0];
        parentCtx.GetProperty("parent").GetProperty("id").GetString().Should().Be("parent1");
        parentCtx.GetProperty("executionMode").GetString().Should().Be("Series");
        parentCtx.GetProperty("position").GetInt32().Should().Be(2);
        parentCtx.GetProperty("totalSiblings").GetInt32().Should().Be(3);

        // Verify previous sibling
        parentCtx.GetProperty("previousSibling").GetProperty("id").GetString().Should().Be("child1");
        parentCtx.GetProperty("previousSibling").GetProperty("title").GetString().Should().Be("First Child");
        parentCtx.GetProperty("previousSibling").GetProperty("status").GetString().Should().Be("Complete");
        parentCtx.GetProperty("previousSibling").GetProperty("type").GetString().Should().Be("Task");

        // Verify next sibling
        parentCtx.GetProperty("nextSibling").GetProperty("id").GetString().Should().Be("child3");
        parentCtx.GetProperty("nextSibling").GetProperty("title").GetString().Should().Be("Third Child");
        parentCtx.GetProperty("nextSibling").GetProperty("status").GetString().Should().Be("Open");
        parentCtx.GetProperty("nextSibling").GetProperty("type").GetString().Should().Be("Feature");
    }

    [Test]
    public void RenderIssueShow_WithParallelParent_OmitsPositionAndSiblings()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent Task")
            .WithExecutionMode(ExecutionMode.Parallel)
            .Build();

        var child = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child Task")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var allIssues = new List<Issue> { parent, child };
        var showContext = IssueHierarchyHelper.BuildShowContext(child, allIssues);

        JsonFormatter.RenderIssueShow(showContext);

        var json = _consoleOutput.ToString().Trim();
        var doc = JsonDocument.Parse(json);
        var parentCtx = doc.RootElement.GetProperty("parents")[0];

        parentCtx.GetProperty("executionMode").GetString().Should().Be("Parallel");
        parentCtx.TryGetProperty("position", out _).Should().BeFalse();
        parentCtx.TryGetProperty("totalSiblings", out _).Should().BeFalse();
        parentCtx.TryGetProperty("previousSibling", out _).Should().BeFalse();
        parentCtx.TryGetProperty("nextSibling", out _).Should().BeFalse();
    }

    [Test]
    public void RenderIssueShow_WithChildren_IncludesChildList()
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
            .WithPriority(1)
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

        JsonFormatter.RenderIssueShow(showContext);

        var json = _consoleOutput.ToString().Trim();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify children
        var children = root.GetProperty("children");
        children.GetArrayLength().Should().Be(2);
        children[0].GetProperty("id").GetString().Should().Be("child1");
        children[0].GetProperty("title").GetString().Should().Be("First Child");
        children[0].GetProperty("status").GetString().Should().Be("Open");
        children[0].GetProperty("type").GetString().Should().Be("Task");
        children[0].GetProperty("priority").GetInt32().Should().Be(1);
        children[1].GetProperty("id").GetString().Should().Be("child2");

        // Verify execution mode
        root.GetProperty("executionMode").GetString().Should().Be("Series");
    }

    [Test]
    public void RenderIssueShow_NoParentsNoChildren_HasEmptyCollections()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Standalone issue")
            .Build();

        var allIssues = new List<Issue> { issue };
        var showContext = IssueHierarchyHelper.BuildShowContext(issue, allIssues);

        JsonFormatter.RenderIssueShow(showContext);

        var json = _consoleOutput.ToString().Trim();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("issue").GetProperty("id").GetString().Should().Be("abc123");
        root.GetProperty("parents").GetArrayLength().Should().Be(0);
        root.GetProperty("children").GetArrayLength().Should().Be(0);
    }

    [Test]
    public void RenderIssueShow_JsonIsValidAndDeserializable()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var allIssues = new List<Issue> { parent, child };
        var showContext = IssueHierarchyHelper.BuildShowContext(child, allIssues);

        JsonFormatter.RenderIssueShow(showContext);

        var json = _consoleOutput.ToString().Trim();

        // Verify round-trip deserialization works
        var deserialized = JsonSerializer.Deserialize(json, FleeceJsonContext.Default.IssueShowDto);
        deserialized.Should().NotBeNull();
        deserialized!.Issue.Id.Should().Be("child1");
        deserialized.Parents.Should().HaveCount(1);
        deserialized.Parents[0].Parent.Id.Should().Be("parent1");
    }

    [Test]
    public void RenderIssue_Verbose_OutputsRawIssueModel()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Test issue")
            .WithStatus(IssueStatus.Progress)
            .WithType(IssueType.Feature)
            .WithPriority(2)
            .WithDescription("A test description")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        JsonFormatter.RenderIssue(issue, verbose: true);

        var json = _consoleOutput.ToString().Trim();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verbose mode should output the raw Issue model with all metadata
        root.GetProperty("id").GetString().Should().Be("abc123");
        root.GetProperty("title").GetString().Should().Be("Test issue");
        root.GetProperty("status").GetString().Should().Be("Progress");
        root.GetProperty("type").GetString().Should().Be("Feature");
        root.GetProperty("priority").GetInt32().Should().Be(2);
        root.GetProperty("description").GetString().Should().Be("A test description");

        // Verbose should include the raw metadata timestamps
        root.TryGetProperty("titleLastUpdate", out _).Should().BeTrue();
        root.TryGetProperty("statusLastUpdate", out _).Should().BeTrue();
        root.TryGetProperty("typeLastUpdate", out _).Should().BeTrue();
    }

    [Test]
    public void RenderIssue_Verbose_DoesNotIncludeHierarchyContext()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var child = new IssueBuilder()
            .WithId("child1")
            .WithTitle("Child")
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        // Even though child has a parent, verbose mode renders the raw Issue,
        // not the enriched IssueShowDto
        JsonFormatter.RenderIssue(child, verbose: true);

        var json = _consoleOutput.ToString().Trim();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("id").GetString().Should().Be("child1");

        // Verbose mode should NOT have parents/children hierarchy context
        root.TryGetProperty("parents", out _).Should().BeFalse();
        root.TryGetProperty("children", out _).Should().BeFalse();

        // But it should have the raw parentIssues field
        root.TryGetProperty("parentIssues", out var parentIssues).Should().BeTrue();
        parentIssues.GetArrayLength().Should().Be(1);
        parentIssues[0].GetProperty("parentIssue").GetString().Should().Be("parent1");
    }

    [Test]
    public void RenderIssue_NonVerbose_OutputsIssueDtoWithoutMetadata()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Test issue")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .Build();

        JsonFormatter.RenderIssue(issue, verbose: false);

        var json = _consoleOutput.ToString().Trim();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("id").GetString().Should().Be("abc123");
        root.GetProperty("title").GetString().Should().Be("Test issue");

        // Non-verbose should NOT include raw metadata timestamps
        root.TryGetProperty("titleLastUpdate", out _).Should().BeFalse();
        root.TryGetProperty("statusLastUpdate", out _).Should().BeFalse();
    }

    [Test]
    public void RenderIssueShow_FirstChildInSeries_NoPreviousSibling()
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

        JsonFormatter.RenderIssueShow(showContext);

        var json = _consoleOutput.ToString().Trim();
        var doc = JsonDocument.Parse(json);
        var parentCtx = doc.RootElement.GetProperty("parents")[0];

        parentCtx.GetProperty("position").GetInt32().Should().Be(1);
        parentCtx.GetProperty("totalSiblings").GetInt32().Should().Be(2);
        parentCtx.TryGetProperty("previousSibling", out _).Should().BeFalse();
        parentCtx.GetProperty("nextSibling").GetProperty("id").GetString().Should().Be("child2");
    }

    [Test]
    public void RenderIssueShow_LastChildInSeries_NoNextSibling()
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

        JsonFormatter.RenderIssueShow(showContext);

        var json = _consoleOutput.ToString().Trim();
        var doc = JsonDocument.Parse(json);
        var parentCtx = doc.RootElement.GetProperty("parents")[0];

        parentCtx.GetProperty("position").GetInt32().Should().Be(2);
        parentCtx.GetProperty("totalSiblings").GetInt32().Should().Be(2);
        parentCtx.GetProperty("previousSibling").GetProperty("id").GetString().Should().Be("child1");
        parentCtx.TryGetProperty("nextSibling", out _).Should().BeFalse();
    }

    [Test]
    public void RenderIssueShow_ChildrenSortedBySortOrderThenPriorityThenTitle()
    {
        var parent = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Parent")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var childA = new IssueBuilder()
            .WithId("childA")
            .WithTitle("Zulu")
            .WithPriority(3)
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var childB = new IssueBuilder()
            .WithId("childB")
            .WithTitle("Alpha")
            .WithPriority(1)
            .WithParentIssueIdAndOrder("parent1", "aaa")
            .Build();

        var childC = new IssueBuilder()
            .WithId("childC")
            .WithTitle("Beta")
            .WithParentIssueIdAndOrder("parent1", "aab")
            .Build();

        var allIssues = new List<Issue> { parent, childA, childB, childC };
        var showContext = IssueHierarchyHelper.BuildShowContext(parent, allIssues);

        JsonFormatter.RenderIssueShow(showContext);

        var json = _consoleOutput.ToString().Trim();
        var doc = JsonDocument.Parse(json);
        var children = doc.RootElement.GetProperty("children");

        // Same sort order "aaa": childB (P1) before childA (P3)
        // Then sort order "aab": childC
        children.GetArrayLength().Should().Be(3);
        children[0].GetProperty("id").GetString().Should().Be("childB");
        children[1].GetProperty("id").GetString().Should().Be("childA");
        children[2].GetProperty("id").GetString().Should().Be("childC");
    }

    [Test]
    public void RenderIssueShow_MultipleParents_IncludesContextForEach()
    {
        var parent1 = new IssueBuilder()
            .WithId("parent1")
            .WithTitle("Series Parent")
            .WithExecutionMode(ExecutionMode.Series)
            .Build();

        var parent2 = new IssueBuilder()
            .WithId("parent2")
            .WithTitle("Parallel Parent")
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
        var showContext = IssueHierarchyHelper.BuildShowContext(child, allIssues);

        JsonFormatter.RenderIssueShow(showContext);

        var json = _consoleOutput.ToString().Trim();
        var doc = JsonDocument.Parse(json);
        var parents = doc.RootElement.GetProperty("parents");

        parents.GetArrayLength().Should().Be(2);
        parents[0].GetProperty("parent").GetProperty("id").GetString().Should().Be("parent1");
        parents[0].GetProperty("executionMode").GetString().Should().Be("Series");
        parents[1].GetProperty("parent").GetProperty("id").GetString().Should().Be("parent2");
        parents[1].GetProperty("executionMode").GetString().Should().Be("Parallel");
    }
}
