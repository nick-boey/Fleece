using Fleece.Core.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Models;

[TestFixture]
public class ParentIssueRefTests
{
    [Test]
    public void Create_WithValidValues_SetsProperties()
    {
        var sut = new ParentIssueRef
        {
            ParentIssue = "abc123",
            SortOrder = "aaa"
        };

        sut.ParentIssue.Should().Be("abc123");
        sut.SortOrder.Should().Be("aaa");
    }

    [Test]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        var ref1 = new ParentIssueRef { ParentIssue = "abc123", SortOrder = "aaa" };
        var ref2 = new ParentIssueRef { ParentIssue = "abc123", SortOrder = "aaa" };

        ref1.Should().Be(ref2);
    }

    [Test]
    public void Equals_WithDifferentParentIssue_ReturnsFalse()
    {
        var ref1 = new ParentIssueRef { ParentIssue = "abc123", SortOrder = "aaa" };
        var ref2 = new ParentIssueRef { ParentIssue = "def456", SortOrder = "aaa" };

        ref1.Should().NotBe(ref2);
    }

    [Test]
    public void Equals_WithDifferentSortOrder_ReturnsFalse()
    {
        var ref1 = new ParentIssueRef { ParentIssue = "abc123", SortOrder = "aaa" };
        var ref2 = new ParentIssueRef { ParentIssue = "abc123", SortOrder = "bbb" };

        ref1.Should().NotBe(ref2);
    }

    [Test]
    public void ParseFromString_WithoutSortOrder_GeneratesSortOrder()
    {
        var result = ParentIssueRef.ParseFromString("abc123", "aaa");

        result.ParentIssue.Should().Be("abc123");
        result.SortOrder.Should().Be("aaa");
    }

    [Test]
    public void ParseFromString_WithSortOrder_UsesSortOrder()
    {
        var result = ParentIssueRef.ParseFromString("abc123:bbb", null);

        result.ParentIssue.Should().Be("abc123");
        result.SortOrder.Should().Be("bbb");
    }

    [Test]
    public void ParseFromStrings_WithMultipleValues_ParsesAll()
    {
        var input = "abc123,def456:bbb,ghi789";
        var result = ParentIssueRef.ParseFromStrings(input);

        result.Should().HaveCount(3);
        result[0].ParentIssue.Should().Be("abc123");
        result[0].SortOrder.Should().Be("aaa");
        result[1].ParentIssue.Should().Be("def456");
        result[1].SortOrder.Should().Be("bbb");
        result[2].ParentIssue.Should().Be("ghi789");
        result[2].SortOrder.Should().Be("aab");
    }

    [Test]
    public void ParseFromStrings_WithEmptyString_ReturnsEmptyList()
    {
        var result = ParentIssueRef.ParseFromStrings("");

        result.Should().BeEmpty();
    }

    [Test]
    public void ParseFromStrings_WithNull_ReturnsEmptyList()
    {
        var result = ParentIssueRef.ParseFromStrings(null);

        result.Should().BeEmpty();
    }
}
