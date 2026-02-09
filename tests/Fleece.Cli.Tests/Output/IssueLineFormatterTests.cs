using Fleece.Cli.Output;
using Fleece.Core.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Cli.Tests.Output;

[TestFixture]
public class IssueLineFormatterTests
{
    [Test]
    public void FormatMarkup_Issue_WithPriority_IncludesPriorityTag()
    {
        var issue = new Fleece.Core.Tests.TestHelpers.IssueBuilder()
            .WithId("abc123")
            .WithTitle("Test issue")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .WithPriority(2)
            .Build();

        var result = IssueLineFormatter.FormatMarkup(issue);

        result.Should().Contain("abc123");
        result.Should().Contain("[task]");
        result.Should().Contain("[open]");
        result.Should().Contain("P2");
        result.Should().Contain("Test issue");
    }

    [Test]
    public void FormatMarkup_Issue_WithoutPriority_OmitsPriorityTag()
    {
        var issue = new Fleece.Core.Tests.TestHelpers.IssueBuilder()
            .WithId("abc123")
            .WithTitle("Test issue")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .Build();

        var result = IssueLineFormatter.FormatMarkup(issue);

        result.Should().Contain("abc123");
        result.Should().NotContain("P");
        result.Should().Contain("Test issue");
    }

    [Test]
    public void FormatMarkup_IssueSummaryDto_WithPriority_IncludesPriorityTag()
    {
        var summary = new IssueSummaryDto
        {
            Id = "abc123",
            Title = "Test issue",
            Status = IssueStatus.Progress,
            Type = IssueType.Bug,
            Priority = 1
        };

        var result = IssueLineFormatter.FormatMarkup(summary);

        result.Should().Contain("abc123");
        result.Should().Contain("[bug]");
        result.Should().Contain("[progress]");
        result.Should().Contain("P1");
        result.Should().Contain("Test issue");
    }

    [Test]
    public void FormatMarkup_IssueSummaryDto_WithoutPriority_OmitsPriorityTag()
    {
        var summary = new IssueSummaryDto
        {
            Id = "abc123",
            Title = "Test issue",
            Status = IssueStatus.Complete,
            Type = IssueType.Feature
        };

        var result = IssueLineFormatter.FormatMarkup(summary);

        result.Should().Contain("abc123");
        result.Should().NotContain("P");
    }

    [Test]
    public void FormatPlainText_ReturnsCorrectFormat()
    {
        var issue = new Fleece.Core.Tests.TestHelpers.IssueBuilder()
            .WithId("abc123")
            .WithTitle("Test issue")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .Build();

        var result = IssueLineFormatter.FormatPlainText(issue);

        result.Should().Be("abc123 open task Test issue");
    }

    [Test]
    public void FormatPlainText_LowercasesStatusAndType()
    {
        var issue = new Fleece.Core.Tests.TestHelpers.IssueBuilder()
            .WithId("def456")
            .WithTitle("Bug fix")
            .WithStatus(IssueStatus.Progress)
            .WithType(IssueType.Bug)
            .Build();

        var result = IssueLineFormatter.FormatPlainText(issue);

        result.Should().Be("def456 progress bug Bug fix");
    }

    [Test]
    public void FormatMarkup_SpecialCharactersInTitle_AreEscaped()
    {
        var issue = new Fleece.Core.Tests.TestHelpers.IssueBuilder()
            .WithId("abc123")
            .WithTitle("Fix [critical] bug & <test>")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Bug)
            .Build();

        var result = IssueLineFormatter.FormatMarkup(issue);

        // Spectre.Console markup special chars should be escaped
        result.Should().Contain("Fix [[critical]] bug & <test>");
    }

    [TestCase(IssueStatus.Open, "cyan")]
    [TestCase(IssueStatus.Progress, "blue")]
    [TestCase(IssueStatus.Review, "purple")]
    [TestCase(IssueStatus.Complete, "green")]
    [TestCase(IssueStatus.Archived, "dim")]
    [TestCase(IssueStatus.Closed, "dim")]
    public void GetStatusColor_ReturnsExpectedColor(IssueStatus status, string expectedColor)
    {
        IssueLineFormatter.GetStatusColor(status).Should().Be(expectedColor);
    }
}
