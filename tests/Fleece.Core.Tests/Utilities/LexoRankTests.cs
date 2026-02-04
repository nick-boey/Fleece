using Fleece.Core.Utilities;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Utilities;

[TestFixture]
public class LexoRankTests
{
    [Test]
    public void GenerateInitialRanks_WithZeroCount_ReturnsEmptyArray()
    {
        var result = LexoRank.GenerateInitialRanks(0);

        result.Should().BeEmpty();
    }

    [Test]
    public void GenerateInitialRanks_WithOne_ReturnsAaa()
    {
        var result = LexoRank.GenerateInitialRanks(1);

        result.Should().Equal(["aaa"]);
    }

    [Test]
    public void GenerateInitialRanks_WithThree_ReturnsSequentialRanks()
    {
        var result = LexoRank.GenerateInitialRanks(3);

        result.Should().Equal(["aaa", "aab", "aac"]);
    }

    [Test]
    public void GenerateInitialRanks_WithTwentySix_HandlesWrapAround()
    {
        var result = LexoRank.GenerateInitialRanks(26);

        result[0].Should().Be("aaa");
        result[25].Should().Be("aaz");
    }

    [Test]
    public void GenerateInitialRanks_WithTwentySeven_IncrementesMiddleCharacter()
    {
        var result = LexoRank.GenerateInitialRanks(27);

        result[26].Should().Be("aba");
    }

    [Test]
    public void GenerateInitialRanks_RanksAreSorted()
    {
        var result = LexoRank.GenerateInitialRanks(100);

        result.Should().BeInAscendingOrder();
    }

    [Test]
    public void GetMiddleRank_BothNull_ReturnsMiddleOfRange()
    {
        var result = LexoRank.GetMiddleRank(null, null);

        result.Should().Be("nnn");
    }

    [Test]
    public void GetMiddleRank_BeforeOnly_ReturnsRankAfter()
    {
        var result = LexoRank.GetMiddleRank("aaa", null);

        string.Compare(result, "aaa", StringComparison.Ordinal).Should().BeGreaterThan(0);
    }

    [Test]
    public void GetMiddleRank_AfterOnly_ReturnsRankBefore()
    {
        var result = LexoRank.GetMiddleRank(null, "zzz");

        string.Compare(result, "zzz", StringComparison.Ordinal).Should().BeLessThan(0);
    }

    [Test]
    public void GetMiddleRank_BetweenTwoRanks_ReturnsMiddle()
    {
        var result = LexoRank.GetMiddleRank("aaa", "aac");

        result.Should().Be("aab");
    }

    [Test]
    public void GetMiddleRank_BetweenAdjacentRanks_ExtendsPrecision()
    {
        var result = LexoRank.GetMiddleRank("aaa", "aab");

        string.Compare(result, "aaa", StringComparison.Ordinal).Should().BeGreaterThan(0);
        string.Compare(result, "aab", StringComparison.Ordinal).Should().BeLessThan(0);
    }

    [Test]
    public void GetMiddleRank_ResultIsBetweenInputs()
    {
        var result = LexoRank.GetMiddleRank("abc", "def");

        string.Compare(result, "abc", StringComparison.Ordinal).Should().BeGreaterThan(0);
        string.Compare(result, "def", StringComparison.Ordinal).Should().BeLessThan(0);
    }

    [Test]
    public void GetMiddleRank_InvalidOrder_ThrowsArgumentException()
    {
        var act = () => LexoRank.GetMiddleRank("zzz", "aaa");

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void GetMiddleRank_EqualRanks_ThrowsArgumentException()
    {
        var act = () => LexoRank.GetMiddleRank("abc", "abc");

        act.Should().Throw<ArgumentException>();
    }
}
