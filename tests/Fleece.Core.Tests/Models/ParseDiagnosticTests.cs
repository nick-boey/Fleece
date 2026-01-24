using Fleece.Core.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Models;

[TestFixture]
public class ParseDiagnosticTests
{
    [Test]
    public void HasIssues_WhenNoUnknownPropertiesAndNoParseErrors_ReturnsFalse()
    {
        var diagnostic = new ParseDiagnostic
        {
            FilePath = "test.jsonl",
            TotalRows = 5,
            ParsedRows = 5,
            FailedRows = 0
        };

        diagnostic.HasIssues.Should().BeFalse();
    }

    [Test]
    public void HasIssues_WhenUnknownPropertiesExist_ReturnsTrue()
    {
        var diagnostic = new ParseDiagnostic
        {
            FilePath = "test.jsonl",
            TotalRows = 5,
            ParsedRows = 5,
            FailedRows = 0,
            UnknownProperties = new HashSet<string> { "unknownField" }
        };

        diagnostic.HasIssues.Should().BeTrue();
    }

    [Test]
    public void HasIssues_WhenParseErrorsExist_ReturnsTrue()
    {
        var diagnostic = new ParseDiagnostic
        {
            FilePath = "test.jsonl",
            TotalRows = 5,
            ParsedRows = 4,
            FailedRows = 1,
            ParseErrors = ["Line 3: Invalid JSON"]
        };

        diagnostic.HasIssues.Should().BeTrue();
    }

    [Test]
    public void SkippedRows_CalculatesCorrectly()
    {
        var diagnostic = new ParseDiagnostic
        {
            FilePath = "test.jsonl",
            TotalRows = 10,
            ParsedRows = 7,
            FailedRows = 3
        };

        diagnostic.SkippedRows.Should().Be(3);
    }

    [Test]
    public void SkippedRows_WhenAllRowsParsed_ReturnsZero()
    {
        var diagnostic = new ParseDiagnostic
        {
            FilePath = "test.jsonl",
            TotalRows = 5,
            ParsedRows = 5,
            FailedRows = 0
        };

        diagnostic.SkippedRows.Should().Be(0);
    }
}
