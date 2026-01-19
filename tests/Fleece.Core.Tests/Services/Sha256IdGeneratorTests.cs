using Fleece.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class Sha256IdGeneratorTests
{
    private Sha256IdGenerator _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new Sha256IdGenerator();
    }

    [Test]
    public void Generate_ReturnsExactlySixCharacters()
    {
        var result = _sut.Generate("Test issue");

        result.Should().HaveLength(6);
    }

    [Test]
    public void Generate_ReturnsOnlyBase62Characters()
    {
        var result = _sut.Generate("Test issue");

        result.Should().MatchRegex("^[0-9A-Za-z]{6}$");
    }

    [Test]
    public void Generate_IsDeterministic_SameTitleProducesSameId()
    {
        var result1 = _sut.Generate("Fix the login bug");
        var result2 = _sut.Generate("Fix the login bug");

        result1.Should().Be(result2);
    }

    [Test]
    public void Generate_NormalizesWhitespace_TrimsTitle()
    {
        var result1 = _sut.Generate("Test issue");
        var result2 = _sut.Generate("  Test issue  ");

        result1.Should().Be(result2);
    }

    [Test]
    public void Generate_NormalizesCase_CaseInsensitive()
    {
        var result1 = _sut.Generate("Test Issue");
        var result2 = _sut.Generate("test issue");
        var result3 = _sut.Generate("TEST ISSUE");

        result1.Should().Be(result2);
        result2.Should().Be(result3);
    }

    [Test]
    public void Generate_DifferentTitles_ProduceDifferentIds()
    {
        var result1 = _sut.Generate("First issue");
        var result2 = _sut.Generate("Second issue");

        result1.Should().NotBe(result2);
    }

    [Test]
    public void Generate_ThrowsOnNullTitle()
    {
        var act = () => _sut.Generate(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Generate_ThrowsOnEmptyTitle()
    {
        var act = () => _sut.Generate("");

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Generate_ThrowsOnWhitespaceOnlyTitle()
    {
        var act = () => _sut.Generate("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Generate_HandlesUnicodeCharacters()
    {
        var result = _sut.Generate("Test issue with unicode: \u00e9\u00e0\u00fc");

        result.Should().HaveLength(6);
        result.Should().MatchRegex("^[0-9A-Za-z]{6}$");
    }
}
