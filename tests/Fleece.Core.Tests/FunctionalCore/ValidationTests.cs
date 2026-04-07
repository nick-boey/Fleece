using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.FunctionalCore;

[TestFixture]
public class ValidationTests
{
    [Test]
    public void ValidateDependencyCycles_NoCycles_ReturnsValid()
    {
        // Arrange: Linear chain A -> B -> C (no cycle)
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithParentIssueIds("B").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("C").Build(),
            new IssueBuilder().WithId("C").Build()
        };

        // Act
        var result = Validation.ValidateDependencyCycles(issues);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Cycles.Should().BeEmpty();
    }

    [Test]
    public void ValidateDependencyCycles_SimpleCycle_ReturnsCycle()
    {
        // Arrange: A -> B -> A (cycle)
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithParentIssueIds("B").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("A").Build()
        };

        // Act
        var result = Validation.ValidateDependencyCycles(issues);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Cycles.Should().HaveCount(1);
        result.Cycles[0].IssueIds.Should().HaveCount(3); // A -> B -> A
    }

    [Test]
    public void ValidateDependencyCycles_SelfReference_ReturnsCycle()
    {
        // Arrange: A -> A (self-reference)
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithParentIssueIds("A").Build()
        };

        // Act
        var result = Validation.ValidateDependencyCycles(issues);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Cycles.Should().HaveCount(1);
        result.Cycles[0].IssueIds.Should().BeEquivalentTo(["A", "A"]);
    }

    [Test]
    public void ValidateDependencyCycles_MultipleCycles_ReturnsAllCycles()
    {
        // Arrange: Two independent cycles: A -> B -> A, C -> D -> C
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithParentIssueIds("B").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("A").Build(),
            new IssueBuilder().WithId("C").WithParentIssueIds("D").Build(),
            new IssueBuilder().WithId("D").WithParentIssueIds("C").Build()
        };

        // Act
        var result = Validation.ValidateDependencyCycles(issues);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Cycles.Should().HaveCount(2);
    }

    [Test]
    public void ValidateDependencyCycles_LongerCycle_ReturnsCycle()
    {
        // Arrange: A -> B -> C -> D -> A (longer cycle)
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithParentIssueIds("B").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("C").Build(),
            new IssueBuilder().WithId("C").WithParentIssueIds("D").Build(),
            new IssueBuilder().WithId("D").WithParentIssueIds("A").Build()
        };

        // Act
        var result = Validation.ValidateDependencyCycles(issues);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Cycles.Should().HaveCount(1);
        result.Cycles[0].IssueIds.Should().HaveCount(5); // A -> B -> C -> D -> A
    }

    [Test]
    public void ValidateDependencyCycles_NoDependencies_ReturnsValid()
    {
        // Arrange: Issues with no ParentIssues
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").Build(),
            new IssueBuilder().WithId("B").Build(),
            new IssueBuilder().WithId("C").Build()
        };

        // Act
        var result = Validation.ValidateDependencyCycles(issues);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Cycles.Should().BeEmpty();
    }

    [Test]
    public void ValidateDependencyCycles_MissingReference_IgnoresOrphan()
    {
        // Arrange: A -> B -> NonExistent (non-existent reference should be ignored)
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithParentIssueIds("B").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("NonExistent").Build()
        };

        // Act
        var result = Validation.ValidateDependencyCycles(issues);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Cycles.Should().BeEmpty();
    }

    [Test]
    public void ValidateDependencyCycles_CaseInsensitive_DetectsCycle()
    {
        // Arrange: Case-insensitive cycle detection
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("abc").WithParentIssueIds("DEF").Build(),
            new IssueBuilder().WithId("DEF").WithParentIssueIds("ABC").Build()
        };

        // Act
        var result = Validation.ValidateDependencyCycles(issues);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Cycles.Should().HaveCount(1);
    }

    [Test]
    public void ValidateDependencyCycles_EmptyIssueList_ReturnsValid()
    {
        // Arrange
        var issues = new List<Issue>();

        // Act
        var result = Validation.ValidateDependencyCycles(issues);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Cycles.Should().BeEmpty();
    }

    [Test]
    public void ValidateDependencyCycles_DiamondPattern_NoCycle()
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
            new IssueBuilder().WithId("A").WithParentIssueIds("B", "C").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("D").Build(),
            new IssueBuilder().WithId("C").WithParentIssueIds("D").Build(),
            new IssueBuilder().WithId("D").Build()
        };

        // Act
        var result = Validation.ValidateDependencyCycles(issues);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Cycles.Should().BeEmpty();
    }

    [Test]
    public void ValidateDependencyCycles_CycleWithTail_DetectsCycleOnly()
    {
        // Arrange: X -> A -> B -> C -> B (cycle B -> C -> B, with tail X -> A)
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("X").WithParentIssueIds("A").Build(),
            new IssueBuilder().WithId("A").WithParentIssueIds("B").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("C").Build(),
            new IssueBuilder().WithId("C").WithParentIssueIds("B").Build()
        };

        // Act
        var result = Validation.ValidateDependencyCycles(issues);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Cycles.Should().HaveCount(1);
        // The cycle should be B -> C -> B (not include X or A)
        result.Cycles[0].IssueIds.Should().HaveCount(3);
    }

    [Test]
    public void WouldCreateCycle_SelfReference_ReturnsTrue()
    {
        var issues = new List<Issue>();

        var result = Validation.WouldCreateCycle(issues, "abc123", "abc123");

        result.Should().BeTrue();
    }

    [Test]
    public void WouldCreateCycle_SelfReference_CaseInsensitive_ReturnsTrue()
    {
        var issues = new List<Issue>();

        var result = Validation.WouldCreateCycle(issues, "ABC123", "abc123");

        result.Should().BeTrue();
    }

    [Test]
    public void WouldCreateCycle_NoExistingEdges_ReturnsFalse()
    {
        // A and B exist with no parent edges
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").Build(),
            new IssueBuilder().WithId("B").Build()
        };

        var result = Validation.WouldCreateCycle(issues, "A", "B");

        result.Should().BeFalse();
    }

    [Test]
    public void WouldCreateCycle_DirectReverseEdge_ReturnsTrue()
    {
        // B already has A as a parent. Adding B as parent of A would create A->B->A.
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("A").Build()
        };

        // Proposing: parent=B, child=A (i.e. A would list B as parent)
        // BFS from B follows B's parents: B->A. If we reach A (the child), it's a cycle.
        // But wait - B's parent is A. BFS from B (the proposed parent) goes to A.
        // The child is A. So childId "A" is reached -> cycle.
        var result = Validation.WouldCreateCycle(issues, "B", "A");

        result.Should().BeTrue();
    }

    [Test]
    public void WouldCreateCycle_TransitiveCycle_ReturnsTrue()
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

        // BFS from C (parent) follows: C->B->A. A is the child -> cycle
        var result = Validation.WouldCreateCycle(issues, "C", "A");

        result.Should().BeTrue();
    }

    [Test]
    public void WouldCreateCycle_DiamondNoCycle_ReturnsFalse()
    {
        // Diamond: A->B, A->C, B->D, C->D
        // Proposing parent=D, child=A: BFS from D has no parents -> no cycle
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithParentIssueIds("B", "C").Build(),
            new IssueBuilder().WithId("B").WithParentIssueIds("D").Build(),
            new IssueBuilder().WithId("C").WithParentIssueIds("D").Build(),
            new IssueBuilder().WithId("D").Build()
        };

        var result = Validation.WouldCreateCycle(issues, "D", "A");

        result.Should().BeFalse();
    }

    [Test]
    public void WouldCreateCycle_LongChainNoCycle_ReturnsFalse()
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

        // BFS from A follows: A->B->C->D->E. Child is E -> reached -> cycle!
        // Actually this IS a cycle: making E a child of A means E->A->B->C->D->E
        var result = Validation.WouldCreateCycle(issues, "A", "E");

        result.Should().BeTrue();
    }

    [Test]
    public void WouldCreateCycle_LongChainNoCycle_SafeDirection_ReturnsFalse()
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

        // BFS from E: E has no parents -> A not reached -> safe
        var result = Validation.WouldCreateCycle(issues, "E", "A");

        result.Should().BeFalse();
    }

    [Test]
    public void WouldCreateCycle_CaseInsensitive_DetectsCycle()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("abc").WithParentIssueIds("DEF").Build(),
            new IssueBuilder().WithId("DEF").Build()
        };

        // abc has parent DEF. Proposing parent=abc, child=DEF
        // BFS from abc: parents = [DEF]. DEF == child -> cycle
        var result = Validation.WouldCreateCycle(issues, "ABC", "def");

        result.Should().BeTrue();
    }

    [Test]
    public void WouldCreateCycle_UnrelatedIssues_ReturnsFalse()
    {
        var issues = new List<Issue>
        {
            new IssueBuilder().WithId("A").WithParentIssueIds("B").Build(),
            new IssueBuilder().WithId("B").Build(),
            new IssueBuilder().WithId("C").WithParentIssueIds("D").Build(),
            new IssueBuilder().WithId("D").Build()
        };

        // A and C are in separate subgraphs
        var result = Validation.WouldCreateCycle(issues, "A", "C");

        result.Should().BeFalse();
    }
}
