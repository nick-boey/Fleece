using FluentAssertions;
using Fleece.Core.Models;
using NUnit.Framework;

namespace Fleece.Core.Tests.Models;

[TestFixture]
public class KeyedTagTests
{
    #region TryParse Tests

    [TestCase("key=value", "key", "value")]
    [TestCase("hsp-linked-pr=123", "hsp-linked-pr", "123")]
    [TestCase("tag=multi=equals", "tag", "multi=equals")]
    [TestCase("KEY=VALUE", "KEY", "VALUE")]
    public void TryParse_ValidKeyValueTag_ReturnsTrue(string tag, string expectedKey, string expectedValue)
    {
        var result = KeyedTag.TryParse(tag, out var key, out var value);

        result.Should().BeTrue();
        key.Should().Be(expectedKey);
        value.Should().Be(expectedValue);
    }

    [TestCase("")]
    [TestCase("  ")]
    [TestCase("notakeyed")]
    [TestCase("=value")]
    [TestCase("key=")]
    public void TryParse_InvalidTag_ReturnsFalse(string tag)
    {
        var result = KeyedTag.TryParse(tag, out var key, out var value);

        result.Should().BeFalse();
        key.Should().BeNull();
        value.Should().BeNull();
    }

    [Test]
    public void TryParse_NullTag_ReturnsFalse()
    {
        var result = KeyedTag.TryParse(null!, out var key, out var value);

        result.Should().BeFalse();
        key.Should().BeNull();
        value.Should().BeNull();
    }

    #endregion

    #region Create Tests

    [Test]
    public void Create_ValidKeyValue_ReturnsFormattedTag()
    {
        var result = KeyedTag.Create("hsp-linked-pr", "123");

        result.Should().Be("hsp-linked-pr=123");
    }

    [TestCase(null, "value")]
    [TestCase("key", null)]
    [TestCase("", "value")]
    [TestCase("key", "")]
    [TestCase("  ", "value")]
    [TestCase("key", "  ")]
    public void Create_InvalidKeyOrValue_ThrowsArgumentException(string? key, string? value)
    {
        var act = () => KeyedTag.Create(key!, value!);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region GetValues Tests

    [Test]
    public void GetValues_TagsWithMatchingKey_ReturnsAllValues()
    {
        var tags = new[] { "hsp-linked-pr=123", "other=tag", "hsp-linked-pr=456", "simple-tag" };

        var result = KeyedTag.GetValues(tags, "hsp-linked-pr").ToList();

        result.Should().HaveCount(2);
        result.Should().Contain("123");
        result.Should().Contain("456");
    }

    [Test]
    public void GetValues_CaseInsensitiveKey_ReturnsMatches()
    {
        var tags = new[] { "HSP-LINKED-PR=123", "hsp-linked-pr=456" };

        var result = KeyedTag.GetValues(tags, "hsp-linked-pr").ToList();

        result.Should().HaveCount(2);
    }

    [Test]
    public void GetValues_NoMatchingKey_ReturnsEmpty()
    {
        var tags = new[] { "other=tag", "simple-tag" };

        var result = KeyedTag.GetValues(tags, "hsp-linked-pr").ToList();

        result.Should().BeEmpty();
    }

    [Test]
    public void GetValues_NullTags_ReturnsEmpty()
    {
        var result = KeyedTag.GetValues(null, "hsp-linked-pr").ToList();

        result.Should().BeEmpty();
    }

    [Test]
    public void GetValues_EmptyTags_ReturnsEmpty()
    {
        var result = KeyedTag.GetValues(Array.Empty<string>(), "hsp-linked-pr").ToList();

        result.Should().BeEmpty();
    }

    #endregion

    #region SetValues Tests

    [Test]
    public void SetValues_ReplacesExistingValues()
    {
        var tags = new[] { "hsp-linked-pr=123", "other=tag", "hsp-linked-pr=456" };

        var result = KeyedTag.SetValues(tags, "hsp-linked-pr", ["789", "999"]);

        result.Should().HaveCount(3);
        result.Should().Contain("other=tag");
        result.Should().Contain("hsp-linked-pr=789");
        result.Should().Contain("hsp-linked-pr=999");
        result.Should().NotContain("hsp-linked-pr=123");
        result.Should().NotContain("hsp-linked-pr=456");
    }

    [Test]
    public void SetValues_PreservesOtherTags()
    {
        var tags = new[] { "simple-tag", "other=value" };

        var result = KeyedTag.SetValues(tags, "hsp-linked-pr", ["123"]);

        result.Should().HaveCount(3);
        result.Should().Contain("simple-tag");
        result.Should().Contain("other=value");
        result.Should().Contain("hsp-linked-pr=123");
    }

    [Test]
    public void SetValues_NullTags_CreatesNewList()
    {
        var result = KeyedTag.SetValues(null, "hsp-linked-pr", ["123"]);

        result.Should().HaveCount(1);
        result.Should().Contain("hsp-linked-pr=123");
    }

    [Test]
    public void SetValues_EmptyValues_RemovesAllWithKey()
    {
        var tags = new[] { "hsp-linked-pr=123", "other=tag" };

        var result = KeyedTag.SetValues(tags, "hsp-linked-pr", Array.Empty<string>());

        result.Should().HaveCount(1);
        result.Should().Contain("other=tag");
    }

    #endregion

    #region AddValue Tests

    [Test]
    public void AddValue_NewValue_AddsToList()
    {
        var tags = new[] { "existing-tag" };

        var result = KeyedTag.AddValue(tags, "hsp-linked-pr", "123");

        result.Should().HaveCount(2);
        result.Should().Contain("existing-tag");
        result.Should().Contain("hsp-linked-pr=123");
    }

    [Test]
    public void AddValue_DuplicateValue_DoesNotAdd()
    {
        var tags = new[] { "hsp-linked-pr=123" };

        var result = KeyedTag.AddValue(tags, "hsp-linked-pr", "123");

        result.Should().HaveCount(1);
    }

    [Test]
    public void AddValue_CaseInsensitiveDuplicate_DoesNotAdd()
    {
        var tags = new[] { "HSP-LINKED-PR=123" };

        var result = KeyedTag.AddValue(tags, "hsp-linked-pr", "123");

        result.Should().HaveCount(1);
    }

    [Test]
    public void AddValue_NullTags_CreatesNewList()
    {
        var result = KeyedTag.AddValue(null, "hsp-linked-pr", "123");

        result.Should().HaveCount(1);
        result.Should().Contain("hsp-linked-pr=123");
    }

    [Test]
    public void AddValue_EmptyValue_ThrowsArgumentException()
    {
        var tags = new[] { "existing-tag" };

        var act = () => KeyedTag.AddValue(tags, "key", "");

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region RemoveValue Tests

    [Test]
    public void RemoveValue_ExistingValue_RemovesIt()
    {
        var tags = new[] { "hsp-linked-pr=123", "hsp-linked-pr=456", "other=tag" };

        var result = KeyedTag.RemoveValue(tags, "hsp-linked-pr", "123");

        result.Should().HaveCount(2);
        result.Should().Contain("hsp-linked-pr=456");
        result.Should().Contain("other=tag");
        result.Should().NotContain("hsp-linked-pr=123");
    }

    [Test]
    public void RemoveValue_CaseInsensitiveMatch_RemovesIt()
    {
        var tags = new[] { "HSP-LINKED-PR=123" };

        var result = KeyedTag.RemoveValue(tags, "hsp-linked-pr", "123");

        result.Should().BeEmpty();
    }

    [Test]
    public void RemoveValue_NonExistingValue_PreservesList()
    {
        var tags = new[] { "hsp-linked-pr=123", "other=tag" };

        var result = KeyedTag.RemoveValue(tags, "hsp-linked-pr", "999");

        result.Should().HaveCount(2);
        result.Should().Contain("hsp-linked-pr=123");
        result.Should().Contain("other=tag");
    }

    [Test]
    public void RemoveValue_NullTags_ReturnsEmpty()
    {
        var result = KeyedTag.RemoveValue(null, "key", "value");

        result.Should().BeEmpty();
    }

    [Test]
    public void RemoveValue_PreservesNonKeyedTags()
    {
        var tags = new[] { "simple-tag", "hsp-linked-pr=123" };

        var result = KeyedTag.RemoveValue(tags, "hsp-linked-pr", "123");

        result.Should().HaveCount(1);
        result.Should().Contain("simple-tag");
    }

    #endregion

    #region HasKey Tests

    [Test]
    public void HasKey_ExistingKey_ReturnsTrue()
    {
        var tags = new[] { "hsp-linked-pr=123", "other=tag" };

        var result = KeyedTag.HasKey(tags, "hsp-linked-pr");

        result.Should().BeTrue();
    }

    [Test]
    public void HasKey_NonExistingKey_ReturnsFalse()
    {
        var tags = new[] { "other=tag", "simple-tag" };

        var result = KeyedTag.HasKey(tags, "hsp-linked-pr");

        result.Should().BeFalse();
    }

    [Test]
    public void HasKey_NullTags_ReturnsFalse()
    {
        var result = KeyedTag.HasKey(null, "hsp-linked-pr");

        result.Should().BeFalse();
    }

    #endregion

    #region LinkedPrKey Constant Tests

    [Test]
    public void LinkedPrKey_HasExpectedValue()
    {
        KeyedTag.LinkedPrKey.Should().Be("hsp-linked-pr");
    }

    #endregion
}
