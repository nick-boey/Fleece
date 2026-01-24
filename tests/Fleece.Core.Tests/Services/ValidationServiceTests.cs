using Fleece.Core.Models;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class ValidationServiceTests
{
    private IIssueService _issueService = null!;
    private ValidationService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _issueService = Substitute.For<IIssueService>();
        _sut = new ValidationService(_issueService);
    }

    [Test]
    public async Task ValidateDependencyCyclesAsync_NoCycles_ReturnsValid()
    {
        // Arrange: Linear chain A -> B -> C (no cycle)
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithPreviousIssues("B").Build(),
            new IssueBuilder().WithId("B").WithPreviousIssues("C").Build(),
            new IssueBuilder().WithId("C").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // Act
        var result = await _sut.ValidateDependencyCyclesAsync();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Cycles.Should().BeEmpty();
    }

    [Test]
    public async Task ValidateDependencyCyclesAsync_SimpleCycle_ReturnsCycle()
    {
        // Arrange: A -> B -> A (cycle)
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithPreviousIssues("B").Build(),
            new IssueBuilder().WithId("B").WithPreviousIssues("A").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // Act
        var result = await _sut.ValidateDependencyCyclesAsync();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Cycles.Should().HaveCount(1);
        result.Cycles[0].IssueIds.Should().HaveCount(3); // A -> B -> A
    }

    [Test]
    public async Task ValidateDependencyCyclesAsync_SelfReference_ReturnsCycle()
    {
        // Arrange: A -> A (self-reference)
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithPreviousIssues("A").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // Act
        var result = await _sut.ValidateDependencyCyclesAsync();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Cycles.Should().HaveCount(1);
        result.Cycles[0].IssueIds.Should().BeEquivalentTo(["A", "A"]);
    }

    [Test]
    public async Task ValidateDependencyCyclesAsync_MultipleCycles_ReturnsAllCycles()
    {
        // Arrange: Two independent cycles: A -> B -> A, C -> D -> C
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithPreviousIssues("B").Build(),
            new IssueBuilder().WithId("B").WithPreviousIssues("A").Build(),
            new IssueBuilder().WithId("C").WithPreviousIssues("D").Build(),
            new IssueBuilder().WithId("D").WithPreviousIssues("C").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // Act
        var result = await _sut.ValidateDependencyCyclesAsync();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Cycles.Should().HaveCount(2);
    }

    [Test]
    public async Task ValidateDependencyCyclesAsync_LongerCycle_ReturnsCycle()
    {
        // Arrange: A -> B -> C -> D -> A (longer cycle)
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithPreviousIssues("B").Build(),
            new IssueBuilder().WithId("B").WithPreviousIssues("C").Build(),
            new IssueBuilder().WithId("C").WithPreviousIssues("D").Build(),
            new IssueBuilder().WithId("D").WithPreviousIssues("A").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // Act
        var result = await _sut.ValidateDependencyCyclesAsync();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Cycles.Should().HaveCount(1);
        result.Cycles[0].IssueIds.Should().HaveCount(5); // A -> B -> C -> D -> A
    }

    [Test]
    public async Task ValidateDependencyCyclesAsync_NoDependencies_ReturnsValid()
    {
        // Arrange: Issues with no PreviousIssues
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").Build(),
            new IssueBuilder().WithId("B").Build(),
            new IssueBuilder().WithId("C").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // Act
        var result = await _sut.ValidateDependencyCyclesAsync();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Cycles.Should().BeEmpty();
    }

    [Test]
    public async Task ValidateDependencyCyclesAsync_MissingReference_IgnoresOrphan()
    {
        // Arrange: A -> B -> NonExistent (non-existent reference should be ignored)
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithPreviousIssues("B").Build(),
            new IssueBuilder().WithId("B").WithPreviousIssues("NonExistent").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // Act
        var result = await _sut.ValidateDependencyCyclesAsync();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Cycles.Should().BeEmpty();
    }

    [Test]
    public async Task ValidateDependencyCyclesAsync_CaseInsensitive_DetectsCycle()
    {
        // Arrange: Case-insensitive cycle detection
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("abc").WithPreviousIssues("DEF").Build(),
            new IssueBuilder().WithId("DEF").WithPreviousIssues("ABC").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // Act
        var result = await _sut.ValidateDependencyCyclesAsync();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Cycles.Should().HaveCount(1);
    }

    [Test]
    public async Task ValidateDependencyCyclesAsync_EmptyIssueList_ReturnsValid()
    {
        // Arrange
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Issue>());

        // Act
        var result = await _sut.ValidateDependencyCyclesAsync();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Cycles.Should().BeEmpty();
    }

    [Test]
    public async Task ValidateDependencyCyclesAsync_DiamondPattern_NoCycle()
    {
        // Arrange: Diamond pattern (not a cycle)
        //     A
        //    / \
        //   B   C
        //    \ /
        //     D
        // A depends on B and C, B and C both depend on D
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithPreviousIssues("B", "C").Build(),
            new IssueBuilder().WithId("B").WithPreviousIssues("D").Build(),
            new IssueBuilder().WithId("C").WithPreviousIssues("D").Build(),
            new IssueBuilder().WithId("D").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // Act
        var result = await _sut.ValidateDependencyCyclesAsync();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Cycles.Should().BeEmpty();
    }

    [Test]
    public async Task ValidateDependencyCyclesAsync_CycleWithTail_DetectsCycleOnly()
    {
        // Arrange: X -> A -> B -> C -> B (cycle B -> C -> B, with tail X -> A)
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("X").WithPreviousIssues("A").Build(),
            new IssueBuilder().WithId("A").WithPreviousIssues("B").Build(),
            new IssueBuilder().WithId("B").WithPreviousIssues("C").Build(),
            new IssueBuilder().WithId("C").WithPreviousIssues("B").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // Act
        var result = await _sut.ValidateDependencyCyclesAsync();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Cycles.Should().HaveCount(1);
        // The cycle should be B -> C -> B (not include X or A)
        result.Cycles[0].IssueIds.Should().HaveCount(3);
    }
}
