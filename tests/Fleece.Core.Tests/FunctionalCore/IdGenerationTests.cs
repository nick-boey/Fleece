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
        var result = IdGeneration.Generate();

        result.Should().HaveLength(6);
    }

    [Test]
    public void Generate_ReturnsOnlyBase62Characters()
    {
        var result = IdGeneration.Generate();

        result.Should().MatchRegex("^[0-9A-Za-z]{6}$");
    }

    [Test]
    public void Generate_ProducesUniqueIds()
    {
        var ids = new HashSet<string>();
        for (var i = 0; i < 100; i++)
        {
            ids.Add(IdGeneration.Generate());
        }

        ids.Should().HaveCount(100, "all 100 generated IDs should be unique");
    }
}
