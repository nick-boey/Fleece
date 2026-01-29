using Fleece.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class SchemaValidatorTests
{
    private SchemaValidator _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new SchemaValidator();
    }

    [Test]
    public void KnownIssueProperties_ContainsExpectedProperties()
    {
        var known = _sut.KnownIssueProperties;

        known.Should().Contain("id");
        known.Should().Contain("title");
        known.Should().Contain("status");
        known.Should().Contain("type");
        known.Should().Contain("description");
        known.Should().Contain("linkedPR");
        known.Should().Contain("lastUpdate");
    }

    [Test]
    public void KnownIssueProperties_UsesCamelCase()
    {
        var known = _sut.KnownIssueProperties;

        // Verify the stored names are in camelCase format
        known.Should().Contain("titleLastUpdate");
        known.Should().Contain("linkedIssues");

        // Verify all stored names start with lowercase (camelCase)
        foreach (var prop in known)
        {
            char.IsLower(prop[0]).Should().BeTrue($"property '{prop}' should start with lowercase");
        }
    }

    [Test]
    public void ValidateJsonlContent_WithValidContent_ReturnsNoIssues()
    {
        var content = """
            {"id":"abc","title":"Test","status":"Idea","type":"Task","lastUpdate":"2024-01-01T00:00:00Z"}
            {"id":"def","title":"Test 2","status":"Next","type":"Bug","lastUpdate":"2024-01-02T00:00:00Z"}
            """;

        var result = _sut.ValidateJsonlContent("test.jsonl", content);

        result.HasIssues.Should().BeFalse();
        result.TotalRows.Should().Be(2);
        result.ParsedRows.Should().Be(2);
        result.FailedRows.Should().Be(0);
        result.UnknownProperties.Should().BeEmpty();
    }

    [Test]
    public void ValidateJsonlContent_WithUnknownProperty_ReturnsWarning()
    {
        var content = """{"id":"abc","title":"Test","status":"Idea","type":"Task","lastUpdate":"2024-01-01T00:00:00Z","unknownField":"value"}""";

        var result = _sut.ValidateJsonlContent("test.jsonl", content);

        result.HasIssues.Should().BeTrue();
        result.UnknownProperties.Should().Contain("unknownField");
        result.ParsedRows.Should().Be(1);
    }

    [Test]
    public void ValidateJsonlContent_WithMultipleUnknownProperties_ReturnsAll()
    {
        var content = """{"id":"abc","futureField":"x","anotherUnknown":"y","title":"Test","status":"Idea","type":"Task","lastUpdate":"2024-01-01T00:00:00Z"}""";

        var result = _sut.ValidateJsonlContent("test.jsonl", content);

        result.UnknownProperties.Should().HaveCount(2);
        result.UnknownProperties.Should().Contain("futureField");
        result.UnknownProperties.Should().Contain("anotherUnknown");
    }

    [Test]
    public void ValidateJsonlContent_WithInvalidJson_ReturnsParseError()
    {
        var content = "not valid json";

        var result = _sut.ValidateJsonlContent("test.jsonl", content);

        result.HasIssues.Should().BeTrue();
        result.TotalRows.Should().Be(1);
        result.ParsedRows.Should().Be(0);
        result.FailedRows.Should().Be(1);
        result.ParseErrors.Should().HaveCount(1);
        result.ParseErrors[0].Should().StartWith("Line 1:");
    }

    [Test]
    public void ValidateJsonlContent_WithMixedContent_ReportsAll()
    {
        var content = """
            {"id":"abc","title":"Valid","status":"Idea","type":"Task","lastUpdate":"2024-01-01T00:00:00Z"}
            invalid json line
            {"id":"def","title":"With Unknown","status":"Next","type":"Bug","lastUpdate":"2024-01-01T00:00:00Z","futureProperty":"x"}
            """;

        var result = _sut.ValidateJsonlContent("test.jsonl", content);

        result.TotalRows.Should().Be(3);
        result.ParsedRows.Should().Be(2);
        result.FailedRows.Should().Be(1);
        result.UnknownProperties.Should().Contain("futureProperty");
        result.ParseErrors.Should().HaveCount(1);
    }

    [Test]
    public void ValidateJsonlContent_WithEmptyLines_IgnoresThem()
    {
        var content = """
            {"id":"abc","title":"Test","status":"Idea","type":"Task","lastUpdate":"2024-01-01T00:00:00Z"}

            {"id":"def","title":"Test 2","status":"Next","type":"Bug","lastUpdate":"2024-01-02T00:00:00Z"}
            """;

        var result = _sut.ValidateJsonlContent("test.jsonl", content);

        result.TotalRows.Should().Be(2);
        result.ParsedRows.Should().Be(2);
    }

    [Test]
    public void ValidateJsonlContent_SetsFilePath()
    {
        var result = _sut.ValidateJsonlContent("path/to/file.jsonl", "{}");

        result.FilePath.Should().Be("path/to/file.jsonl");
    }

    [Test]
    public void ExtractUnknownProperties_WithKnownProperties_ReturnsEmpty()
    {
        var json = """{"id":"abc","title":"Test"}""";

        var result = _sut.ExtractUnknownProperties(json);

        result.Should().BeEmpty();
    }

    [Test]
    public void ExtractUnknownProperties_WithUnknownProperty_ReturnsIt()
    {
        var json = """{"id":"abc","unknownField":"value"}""";

        var result = _sut.ExtractUnknownProperties(json);

        result.Should().Contain("unknownField");
    }

    [Test]
    public void ExtractUnknownProperties_WithInvalidJson_ReturnsEmpty()
    {
        var result = _sut.ExtractUnknownProperties("not json");

        result.Should().BeEmpty();
    }

    [Test]
    public void ExtractUnknownProperties_WithEmptyString_ReturnsEmpty()
    {
        var result = _sut.ExtractUnknownProperties("");

        result.Should().BeEmpty();
    }

    [Test]
    public void KnownIssueProperties_IsCaseInsensitive()
    {
        // The Contains check should be case-insensitive
        var json1 = """{"Id":"abc","Title":"Test"}""";
        var json2 = """{"ID":"abc","TITLE":"Test"}""";

        var result1 = _sut.ExtractUnknownProperties(json1);
        var result2 = _sut.ExtractUnknownProperties(json2);

        result1.Should().BeEmpty();
        result2.Should().BeEmpty();
    }
}
