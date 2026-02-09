using Fleece.Core.Models;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class ValidationServiceCycleCheckTests
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
    public async Task WouldCreateCycleAsync_SelfReference_ReturnsTrue()
    {
        var result = await _sut.WouldCreateCycleAsync("abc123", "abc123");

        result.Should().BeTrue();
    }

    [Test]
    public async Task WouldCreateCycleAsync_SelfReference_CaseInsensitive_ReturnsTrue()
    {
        var result = await _sut.WouldCreateCycleAsync("ABC123", "abc123");

        result.Should().BeTrue();
    }

    [Test]
    public async Task WouldCreateCycleAsync_NoExistingEdges_ReturnsFalse()
    {
        // A and B exist with no parent edges
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").Build(),
            new IssueBuilder().WithId("B").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.WouldCreateCycleAsync("A", "B");

        result.Should().BeFalse();
    }

    [Test]
    public async Task WouldCreateCycleAsync_DirectReverseEdge_ReturnsTrue()
    {
        // B already has A as a parent. Adding B as parent of A would create A->B->A.
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("A").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // Proposing: parent=B, child=A (i.e. A would list B as parent)
        // BFS from B follows B's parents: B->A. If we reach A (the child), it's a cycle.
        // But wait - B's parent is A. BFS from B (the proposed parent) goes to A.
        // The child is A. So childId "A" is reached → cycle.
        var result = await _sut.WouldCreateCycleAsync("B", "A");

        result.Should().BeTrue();
    }

    [Test]
    public async Task WouldCreateCycleAsync_TransitiveCycle_ReturnsTrue()
    {
        // Chain: C has parent B, B has parent A.
        // Proposing: parent=A, child=C would not create a cycle (A->B->C is fine, making C child of A)
        // But proposing: parent=C, child=A would create cycle A->B->C->A
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("A").Build(),
            new IssueBuilder().WithId("C").WithParentIssueIds("B").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // BFS from C (parent) follows: C->B->A. A is the child → cycle
        var result = await _sut.WouldCreateCycleAsync("C", "A");

        result.Should().BeTrue();
    }

    [Test]
    public async Task WouldCreateCycleAsync_DiamondNoCycle_ReturnsFalse()
    {
        // Diamond: A->B, A->C, B->D, C->D
        // Proposing parent=D, child=A: BFS from D has no parents → no cycle
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithParentIssueIds("B", "C").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("D").Build(),
            new IssueBuilder().WithId("C").WithParentIssueIds("D").Build(),
            new IssueBuilder().WithId("D").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.WouldCreateCycleAsync("D", "A");

        result.Should().BeFalse();
    }

    [Test]
    public async Task WouldCreateCycleAsync_LongChainNoCycle_ReturnsFalse()
    {
        // Chain: A->B->C->D->E, proposing parent=A, child=E
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithParentIssueIds("B").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("C").Build(),
            new IssueBuilder().WithId("C").WithParentIssueIds("D").Build(),
            new IssueBuilder().WithId("D").WithParentIssueIds("E").Build(),
            new IssueBuilder().WithId("E").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // BFS from A follows: A->B->C->D->E. Child is E → reached → cycle!
        // Actually this IS a cycle: making E a child of A means E->A->B->C->D->E
        var result = await _sut.WouldCreateCycleAsync("A", "E");

        result.Should().BeTrue();
    }

    [Test]
    public async Task WouldCreateCycleAsync_LongChainNoCycle_SafeDirection_ReturnsFalse()
    {
        // Chain: A->B->C->D->E, proposing parent=E, child=A
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithParentIssueIds("B").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("C").Build(),
            new IssueBuilder().WithId("C").WithParentIssueIds("D").Build(),
            new IssueBuilder().WithId("D").WithParentIssueIds("E").Build(),
            new IssueBuilder().WithId("E").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // BFS from E: E has no parents → A not reached → safe
        var result = await _sut.WouldCreateCycleAsync("E", "A");

        result.Should().BeFalse();
    }

    [Test]
    public async Task WouldCreateCycleAsync_CaseInsensitive_DetectsCycle()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("abc").WithParentIssueIds("DEF").Build(),
            new IssueBuilder().WithId("DEF").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // abc has parent DEF. Proposing parent=abc, child=DEF
        // BFS from abc: parents = [DEF]. DEF == child → cycle
        var result = await _sut.WouldCreateCycleAsync("ABC", "def");

        result.Should().BeTrue();
    }

    [Test]
    public async Task WouldCreateCycleAsync_UnrelatedIssues_ReturnsFalse()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithParentIssueIds("B").Build(),
            new IssueBuilder().WithId("B").Build(),
            new IssueBuilder().WithId("C").WithParentIssueIds("D").Build(),
            new IssueBuilder().WithId("D").Build()
        };
        _issueService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(issues);

        // A and C are in separate subgraphs
        var result = await _sut.WouldCreateCycleAsync("A", "C");

        result.Should().BeFalse();
    }
}
