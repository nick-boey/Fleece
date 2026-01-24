using Fleece.Core.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Models;

[TestFixture]
public class LoadIssuesResultTests
{
    [Test]
    public void HasWarnings_WhenNoDiagnostics_ReturnsFalse()
    {
        var result = new LoadIssuesResult();

        result.HasWarnings.Should().BeFalse();
    }

    [Test]
    public void HasWarnings_WhenDiagnosticsHaveNoIssues_ReturnsFalse()
    {
        var result = new LoadIssuesResult
        {
            Diagnostics =
            [
                new ParseDiagnostic
                {
                    FilePath = "test.jsonl",
                    TotalRows = 5,
                    ParsedRows = 5,
                    FailedRows = 0
                }
            ]
        };

        result.HasWarnings.Should().BeFalse();
    }

    [Test]
    public void HasWarnings_WhenAnyDiagnosticHasUnknownProperties_ReturnsTrue()
    {
        var result = new LoadIssuesResult
        {
            Diagnostics =
            [
                new ParseDiagnostic
                {
                    FilePath = "test.jsonl",
                    TotalRows = 5,
                    ParsedRows = 5,
                    FailedRows = 0,
                    UnknownProperties = new HashSet<string> { "unknownField" }
                }
            ]
        };

        result.HasWarnings.Should().BeTrue();
    }

    [Test]
    public void HasWarnings_WhenAnyDiagnosticHasParseErrors_ReturnsTrue()
    {
        var result = new LoadIssuesResult
        {
            Diagnostics =
            [
                new ParseDiagnostic
                {
                    FilePath = "test.jsonl",
                    TotalRows = 5,
                    ParsedRows = 4,
                    FailedRows = 1,
                    ParseErrors = ["Invalid JSON"]
                }
            ]
        };

        result.HasWarnings.Should().BeTrue();
    }

    [Test]
    public void HasWarnings_WithMultipleDiagnostics_ReturnsTrueIfAnyHasIssues()
    {
        var result = new LoadIssuesResult
        {
            Diagnostics =
            [
                new ParseDiagnostic
                {
                    FilePath = "good.jsonl",
                    TotalRows = 5,
                    ParsedRows = 5,
                    FailedRows = 0
                },
                new ParseDiagnostic
                {
                    FilePath = "bad.jsonl",
                    TotalRows = 3,
                    ParsedRows = 2,
                    FailedRows = 1,
                    ParseErrors = ["Parse error"]
                }
            ]
        };

        result.HasWarnings.Should().BeTrue();
    }
}
