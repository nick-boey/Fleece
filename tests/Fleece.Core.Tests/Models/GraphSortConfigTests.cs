using Fleece.Core.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Models;

[TestFixture]
public class GraphSortConfigTests
{
    [Test]
    public void Default_ShouldBeCreatedAtAscending()
    {
        var config = GraphSortConfig.Default;

        config.Rules.Should().HaveCount(1);
        config.Rules[0].Criteria.Should().Be(GraphSortCriteria.CreatedAt);
        config.Rules[0].Direction.Should().Be(SortDirection.Ascending);
    }

    [Test]
    public void Parse_SingleCriteria_DefaultsToAscending()
    {
        var config = GraphSortConfig.Parse("created");

        config.Rules.Should().HaveCount(1);
        config.Rules[0].Criteria.Should().Be(GraphSortCriteria.CreatedAt);
        config.Rules[0].Direction.Should().Be(SortDirection.Ascending);
    }

    [Test]
    public void Parse_SingleCriteriaWithDirection()
    {
        var config = GraphSortConfig.Parse("priority:desc");

        config.Rules.Should().HaveCount(1);
        config.Rules[0].Criteria.Should().Be(GraphSortCriteria.Priority);
        config.Rules[0].Direction.Should().Be(SortDirection.Descending);
    }

    [Test]
    public void Parse_MultipleCriteria()
    {
        var config = GraphSortConfig.Parse("priority:desc,title:asc");

        config.Rules.Should().HaveCount(2);
        config.Rules[0].Criteria.Should().Be(GraphSortCriteria.Priority);
        config.Rules[0].Direction.Should().Be(SortDirection.Descending);
        config.Rules[1].Criteria.Should().Be(GraphSortCriteria.Title);
        config.Rules[1].Direction.Should().Be(SortDirection.Ascending);
    }

    [Test]
    public void Parse_AllCriteria()
    {
        var config = GraphSortConfig.Parse("created,priority,description,title");

        config.Rules.Should().HaveCount(4);
        config.Rules[0].Criteria.Should().Be(GraphSortCriteria.CreatedAt);
        config.Rules[1].Criteria.Should().Be(GraphSortCriteria.Priority);
        config.Rules[2].Criteria.Should().Be(GraphSortCriteria.HasDescription);
        config.Rules[3].Criteria.Should().Be(GraphSortCriteria.Title);
    }

    [Test]
    public void Parse_AlternateNames()
    {
        var config = GraphSortConfig.Parse("createdat,hasdescription");

        config.Rules.Should().HaveCount(2);
        config.Rules[0].Criteria.Should().Be(GraphSortCriteria.CreatedAt);
        config.Rules[1].Criteria.Should().Be(GraphSortCriteria.HasDescription);
    }

    [Test]
    public void Parse_DescendingAlternateForm()
    {
        var config = GraphSortConfig.Parse("title:descending");

        config.Rules.Should().HaveCount(1);
        config.Rules[0].Direction.Should().Be(SortDirection.Descending);
    }

    [Test]
    public void Parse_AscendingAlternateForm()
    {
        var config = GraphSortConfig.Parse("title:ascending");

        config.Rules.Should().HaveCount(1);
        config.Rules[0].Direction.Should().Be(SortDirection.Ascending);
    }

    [Test]
    public void Parse_InvalidCriteria_ThrowsArgumentException()
    {
        var act = () => GraphSortConfig.Parse("invalid");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid sort criteria*");
    }

    [Test]
    public void Parse_InvalidDirection_ThrowsArgumentException()
    {
        var act = () => GraphSortConfig.Parse("title:invalid");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid sort direction*");
    }

    [Test]
    public void Parse_EmptyString_ReturnsDefault()
    {
        var config = GraphSortConfig.Parse("");

        config.Rules.Should().HaveCount(1);
        config.Rules[0].Criteria.Should().Be(GraphSortCriteria.CreatedAt);
    }

    [Test]
    public void Parse_WithWhitespace_TrimsCorrectly()
    {
        var config = GraphSortConfig.Parse(" priority : desc , title : asc ");

        config.Rules.Should().HaveCount(2);
        config.Rules[0].Criteria.Should().Be(GraphSortCriteria.Priority);
        config.Rules[0].Direction.Should().Be(SortDirection.Descending);
        config.Rules[1].Criteria.Should().Be(GraphSortCriteria.Title);
        config.Rules[1].Direction.Should().Be(SortDirection.Ascending);
    }
}
