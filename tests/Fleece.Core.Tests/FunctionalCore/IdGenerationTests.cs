using Fleece.Core.FunctionalCore;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.FunctionalCore;

[TestFixture]
public class IdGenerationTests
{
    [Test]
    public void Generate_ReturnsExactlySixCharacters()
    {
        var result = IdGeneration.Generate("Test issue");

        result.Should().HaveLength(6);
    }

    [Test]
    public void Generate_ReturnsOnlyBase62Characters()
    {
        var result = IdGeneration.Generate("Test issue");

        result.Should().MatchRegex("^[0-9A-Za-z]{6}$");
    }

    [Test]
    public void Generate_IsDeterministic_SameTitleProducesSameId()
    {
        var result1 = IdGeneration.Generate("Fix the login bug");
        var result2 = IdGeneration.Generate("Fix the login bug");

        result1.Should().Be(result2);
    }

    [Test]
    public void Generate_NormalizesWhitespace_TrimsTitle()
    {
        var result1 = IdGeneration.Generate("Test issue");
        var result2 = IdGeneration.Generate("  Test issue  ");

        result1.Should().Be(result2);
    }

    [Test]
    public void Generate_NormalizesCase_CaseInsensitive()
    {
        var result1 = IdGeneration.Generate("Test Issue");
        var result2 = IdGeneration.Generate("test issue");
        var result3 = IdGeneration.Generate("TEST ISSUE");

        result1.Should().Be(result2);
        result2.Should().Be(result3);
    }

    [Test]
    public void Generate_DifferentTitles_ProduceDifferentIds()
    {
        var result1 = IdGeneration.Generate("First issue");
        var result2 = IdGeneration.Generate("Second issue");

        result1.Should().NotBe(result2);
    }

    [Test]
    public void Generate_ThrowsOnNullTitle()
    {
        var act = () => IdGeneration.Generate(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Generate_ThrowsOnEmptyTitle()
    {
        var act = () => IdGeneration.Generate("");

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Generate_ThrowsOnWhitespaceOnlyTitle()
    {
        var act = () => IdGeneration.Generate("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Generate_HandlesUnicodeCharacters()
    {
        var result = IdGeneration.Generate("Test issue with unicode: éàü");

        result.Should().HaveLength(6);
        result.Should().MatchRegex("^[0-9A-Za-z]{6}$");
    }

    [Test]
    public void GenerateWithSalt_ProducesDifferentIdThanUnsalted()
    {
        var unsalted = IdGeneration.Generate("Test issue", 0);
        var salted = IdGeneration.Generate("Test issue", 1);

        salted.Should().NotBe(unsalted);
    }

    [Test]
    public void GenerateWithZeroSalt_MatchesUnsaltedGenerate()
    {
        var unsalted = IdGeneration.Generate("Test issue");
        var zeroSalt = IdGeneration.Generate("Test issue", 0);

        zeroSalt.Should().Be(unsalted);
    }

    [Test]
    public void GenerateWithSalt_IsDeterministic()
    {
        var result1 = IdGeneration.Generate("Test issue", 5);
        var result2 = IdGeneration.Generate("Test issue", 5);

        result1.Should().Be(result2);
    }

    [Test]
    public void GenerateWithSalt_DifferentSaltsProduceDifferentIds()
    {
        var salt1 = IdGeneration.Generate("Test issue", 1);
        var salt2 = IdGeneration.Generate("Test issue", 2);

        salt1.Should().NotBe(salt2);
    }

    [Test]
    public void GenerateWithSalt_ReturnsSixCharBase62()
    {
        var result = IdGeneration.Generate("Test issue", 3);

        result.Should().HaveLength(6);
        result.Should().MatchRegex("^[0-9A-Za-z]{6}$");
    }
}
