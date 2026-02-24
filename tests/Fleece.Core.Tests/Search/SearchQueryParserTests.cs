using Fleece.Core.Search;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Search;

[TestFixture]
public class SearchQueryParserTests
{
    private SearchQueryParser _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new SearchQueryParser();
    }

    #region Basic Parsing

    [Test]
    public void Parse_EmptyString_ReturnsEmptyQuery()
    {
        var result = _sut.Parse("");

        result.IsEmpty.Should().BeTrue();
        result.Tokens.Should().BeEmpty();
    }

    [Test]
    public void Parse_NullString_ReturnsEmptyQuery()
    {
        var result = _sut.Parse(null);

        result.IsEmpty.Should().BeTrue();
        result.Tokens.Should().BeEmpty();
    }

    [Test]
    public void Parse_WhitespaceOnly_ReturnsEmptyQuery()
    {
        var result = _sut.Parse("   \t  ");

        result.IsEmpty.Should().BeTrue();
        result.Tokens.Should().BeEmpty();
    }

    [Test]
    public void Parse_PlainText_ReturnsTextToken()
    {
        var result = _sut.Parse("login");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[0].IsNegated.Should().BeFalse();
        result.Tokens[0].Values.Should().ContainSingle().Which.Should().Be("login");
    }

    [Test]
    public void Parse_MultipleWords_ReturnsMultipleTextTokens()
    {
        var result = _sut.Parse("login authentication");

        result.Tokens.Should().HaveCount(2);
        result.Tokens[0].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[0].Values[0].Should().Be("login");
        result.Tokens[1].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[1].Values[0].Should().Be("authentication");
    }

    #endregion

    #region Field Filters

    [Test]
    public void Parse_StatusFilter_ParsesCorrectly()
    {
        var result = _sut.Parse("status:open");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.StatusFilter);
        result.Tokens[0].IsNegated.Should().BeFalse();
        result.Tokens[0].Values.Should().ContainSingle().Which.Should().Be("open");
    }

    [Test]
    public void Parse_TypeFilter_ParsesCorrectly()
    {
        var result = _sut.Parse("type:bug");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.TypeFilter);
        result.Tokens[0].Values.Should().ContainSingle().Which.Should().Be("bug");
    }

    [Test]
    public void Parse_PriorityFilter_ParsesCorrectly()
    {
        var result = _sut.Parse("priority:1");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.PriorityFilter);
        result.Tokens[0].Values.Should().ContainSingle().Which.Should().Be("1");
    }

    [Test]
    public void Parse_AssignedFilter_ParsesCorrectly()
    {
        var result = _sut.Parse("assigned:john");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.AssignedFilter);
        result.Tokens[0].Values.Should().ContainSingle().Which.Should().Be("john");
    }

    [Test]
    public void Parse_TagFilter_ParsesCorrectly()
    {
        var result = _sut.Parse("tag:backend");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.TagFilter);
        result.Tokens[0].Values.Should().ContainSingle().Which.Should().Be("backend");
    }

    [Test]
    public void Parse_LinkedPrFilter_ParsesCorrectly()
    {
        var result = _sut.Parse("linkedpr:123");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.LinkedPrFilter);
        result.Tokens[0].Values.Should().ContainSingle().Which.Should().Be("123");
    }

    [Test]
    public void Parse_PrAlias_MapsToLinkedPr()
    {
        var result = _sut.Parse("pr:42");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.LinkedPrFilter);
        result.Tokens[0].Values.Should().ContainSingle().Which.Should().Be("42");
    }

    [Test]
    public void Parse_IdFilter_ParsesCorrectly()
    {
        var result = _sut.Parse("id:abc123");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.IdFilter);
        result.Tokens[0].Values.Should().ContainSingle().Which.Should().Be("abc123");
    }

    #endregion

    #region Case Insensitivity

    [Test]
    public void Parse_FieldName_IsCaseInsensitive()
    {
        var result = _sut.Parse("STATUS:open");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.StatusFilter);
    }

    [Test]
    public void Parse_MixedCaseFieldName_IsCaseInsensitive()
    {
        var result = _sut.Parse("StAtUs:open");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.StatusFilter);
    }

    [Test]
    public void Parse_StatusValue_PreservesCase()
    {
        var result = _sut.Parse("status:Open");

        result.Tokens[0].Values[0].Should().Be("Open");
    }

    #endregion

    #region Negation

    [Test]
    public void Parse_NegatedFilter_SetsIsNegated()
    {
        var result = _sut.Parse("-status:open");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.StatusFilter);
        result.Tokens[0].IsNegated.Should().BeTrue();
        result.Tokens[0].Values.Should().ContainSingle().Which.Should().Be("open");
    }

    [Test]
    public void Parse_NegatedText_SetsIsNegated()
    {
        var result = _sut.Parse("-login");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[0].IsNegated.Should().BeTrue();
        result.Tokens[0].Values[0].Should().Be("login");
    }

    [Test]
    public void Parse_NegatedMultiValue_SetsIsNegated()
    {
        var result = _sut.Parse("-type:bug, feature;");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.TypeFilter);
        result.Tokens[0].IsNegated.Should().BeTrue();
        result.Tokens[0].Values.Should().HaveCount(2);
    }

    [Test]
    public void Parse_StandaloneDash_TreatedAsTextToken()
    {
        var result = _sut.Parse("-");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[0].Values[0].Should().Be("-");
    }

    #endregion

    #region Multi-Value

    [Test]
    public void Parse_MultiValue_WithSemicolon_ParsesAllValues()
    {
        var result = _sut.Parse("type:bug, feature;");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.TypeFilter);
        result.Tokens[0].Values.Should().HaveCount(2);
        result.Tokens[0].Values.Should().Contain("bug");
        result.Tokens[0].Values.Should().Contain("feature");
    }

    [Test]
    public void Parse_MultiValue_WithoutSemicolon_TreatsSingleValue()
    {
        // Without semicolon, the entire comma-separated string is treated as a single value
        var result = _sut.Parse("type:bug,feature");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Values.Should().HaveCount(1);
        result.Tokens[0].Values[0].Should().Be("bug,feature"); // Whole string is the value
    }

    [Test]
    public void Parse_MultiValue_TrimsWhitespace()
    {
        var result = _sut.Parse("type:bug, feature, task;");

        result.Tokens[0].Values.Should().HaveCount(3);
        result.Tokens[0].Values[0].Should().Be("bug");
        result.Tokens[0].Values[1].Should().Be("feature");
        result.Tokens[0].Values[2].Should().Be("task");
    }

    [Test]
    public void Parse_MultiValue_WithSpacesInValues()
    {
        var result = _sut.Parse("status:open, in progress;");

        result.Tokens[0].Values.Should().HaveCount(2);
        result.Tokens[0].Values[0].Should().Be("open");
        result.Tokens[0].Values[1].Should().Be("in progress");
    }

    #endregion

    #region Combined Queries

    [Test]
    public void Parse_MixedFilterAndText_ParsesBoth()
    {
        var result = _sut.Parse("status:open login");

        result.Tokens.Should().HaveCount(2);
        result.Tokens[0].Type.Should().Be(SearchTokenType.StatusFilter);
        result.Tokens[0].Values[0].Should().Be("open");
        result.Tokens[1].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[1].Values[0].Should().Be("login");
    }

    [Test]
    public void Parse_MultipleFilters_ParsesAll()
    {
        var result = _sut.Parse("status:open type:bug priority:1");

        result.Tokens.Should().HaveCount(3);
        result.Tokens[0].Type.Should().Be(SearchTokenType.StatusFilter);
        result.Tokens[1].Type.Should().Be(SearchTokenType.TypeFilter);
        result.Tokens[2].Type.Should().Be(SearchTokenType.PriorityFilter);
    }

    [Test]
    public void Parse_TextBeforeAndAfterFilter_ParsesAll()
    {
        var result = _sut.Parse("login status:open authentication");

        result.Tokens.Should().HaveCount(3);
        result.Tokens[0].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[0].Values[0].Should().Be("login");
        result.Tokens[1].Type.Should().Be(SearchTokenType.StatusFilter);
        result.Tokens[2].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[2].Values[0].Should().Be("authentication");
    }

    [Test]
    public void Parse_ComplexQuery_ParsesCorrectly()
    {
        var result = _sut.Parse("status:open -type:bug, chore; login priority:1");

        result.Tokens.Should().HaveCount(4);

        result.Tokens[0].Type.Should().Be(SearchTokenType.StatusFilter);
        result.Tokens[0].IsNegated.Should().BeFalse();
        result.Tokens[0].Values.Should().ContainSingle().Which.Should().Be("open");

        result.Tokens[1].Type.Should().Be(SearchTokenType.TypeFilter);
        result.Tokens[1].IsNegated.Should().BeTrue();
        result.Tokens[1].Values.Should().HaveCount(2);

        result.Tokens[2].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[2].Values[0].Should().Be("login");

        result.Tokens[3].Type.Should().Be(SearchTokenType.PriorityFilter);
    }

    #endregion

    #region Edge Cases

    [Test]
    public void Parse_UnknownField_TreatsAsText()
    {
        var result = _sut.Parse("unknown:value");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[0].Values[0].Should().Be("unknown:value");
    }

    [Test]
    public void Parse_EmptyFieldValue_IgnoresFilter()
    {
        var result = _sut.Parse("status: nextword");

        // "status:" has empty value, so it's ignored
        // "nextword" becomes a text token
        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[0].Values[0].Should().Be("nextword");
    }

    [Test]
    public void Parse_ColonOnly_TreatedAsText()
    {
        var result = _sut.Parse(":");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[0].Values[0].Should().Be(":");
    }

    [Test]
    public void Parse_ColonAtEnd_TreatedAsText()
    {
        var result = _sut.Parse("word:");

        // "word" is not a known field, so "word:" is treated as text
        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.Text);
    }

    [Test]
    public void Parse_MultipleSpaces_HandledCorrectly()
    {
        var result = _sut.Parse("login    authentication");

        result.Tokens.Should().HaveCount(2);
    }

    [Test]
    public void Parse_LeadingAndTrailingSpaces_Trimmed()
    {
        var result = _sut.Parse("  login  ");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Values[0].Should().Be("login");
    }

    [Test]
    public void Parse_TextWithColonInsideWord_TreatedAsText()
    {
        // "foo:bar" where "foo" is not a known field
        var result = _sut.Parse("http://example.com");

        result.Tokens.Should().HaveCount(1);
        result.Tokens[0].Type.Should().Be(SearchTokenType.Text);
        result.Tokens[0].Values[0].Should().Be("http://example.com");
    }

    #endregion
}
