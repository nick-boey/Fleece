using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Tests.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class JsonlSerializerTests
{
    private JsonlSerializer _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new JsonlSerializer();
    }

    [Test]
    public void SerializeIssue_ReturnsValidJson()
    {
        var issue = new IssueBuilder()
            .WithId("abc123")
            .WithTitle("Test Issue")
            .WithStatus(IssueStatus.Open)
            .WithType(IssueType.Task)
            .Build();

        var result = _sut.SerializeIssue(issue);

        result.Should().Contain("\"id\":\"abc123\"");
        result.Should().Contain("\"title\":\"Test Issue\"");
        result.Should().NotContain("\n");
    }

    [Test]
    public void SerializeIssue_UseCamelCase()
    {
        var issue = new IssueBuilder()
            .WithLinkedPr(42)
            .Build();

        var result = _sut.SerializeIssue(issue);

        result.Should().Contain("\"linkedPR\":42");
    }

    [Test]
    public void SerializeIssue_OmitsNullValues()
    {
        var issue = new IssueBuilder()
            .WithDescription(null)
            .WithPriority(null)
            .Build();

        var result = _sut.SerializeIssue(issue);

        result.Should().NotContain("\"description\"");
        result.Should().NotContain("\"priority\"");
    }

    [Test]
    public void DeserializeIssue_ReturnsIssue()
    {
        var json = "{\"id\":\"abc123\",\"title\":\"Test\",\"status\":\"Idea\",\"type\":\"Bug\",\"lastUpdate\":\"2024-01-15T10:30:00+00:00\"}";

        var result = _sut.DeserializeIssue(json);

        result.Should().NotBeNull();
        result!.Id.Should().Be("abc123");
        result.Title.Should().Be("Test");
        result.Status.Should().Be(IssueStatus.Open);
        result.Type.Should().Be(IssueType.Bug);
    }

    [Test]
    public void DeserializeIssue_ReturnsNullForEmptyLine()
    {
        var result = _sut.DeserializeIssue("");

        result.Should().BeNull();
    }

    [Test]
    public void DeserializeIssue_ReturnsNullForWhitespace()
    {
        var result = _sut.DeserializeIssue("   ");

        result.Should().BeNull();
    }

    [Test]
    public void DeserializeIssue_ReturnsNullForInvalidJson()
    {
        var result = _sut.DeserializeIssue("not valid json");

        result.Should().BeNull();
    }

    [Test]
    public void DeserializeIssues_ReturnsEmptyListForEmptyContent()
    {
        var result = _sut.DeserializeIssues("");

        result.Should().BeEmpty();
    }

    [Test]
    public void DeserializeIssues_ParsesMultipleLines()
    {
        var content = "{\"id\":\"abc123\",\"title\":\"Issue 1\",\"status\":\"Idea\",\"type\":\"Task\",\"lastUpdate\":\"2024-01-15T10:30:00+00:00\"}\n" +
                      "{\"id\":\"def456\",\"title\":\"Issue 2\",\"status\":\"Complete\",\"type\":\"Bug\",\"lastUpdate\":\"2024-01-15T11:00:00+00:00\"}";

        var result = _sut.DeserializeIssues(content);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("abc123");
        result[1].Id.Should().Be("def456");
    }

    [Test]
    public void DeserializeIssues_SkipsInvalidLines()
    {
        var content = "{\"id\":\"abc123\",\"title\":\"Issue 1\",\"status\":\"Idea\",\"type\":\"Task\",\"lastUpdate\":\"2024-01-15T10:30:00+00:00\"}\n" +
                      "invalid line\n" +
                      "{\"id\":\"def456\",\"title\":\"Issue 2\",\"status\":\"Complete\",\"type\":\"Bug\",\"lastUpdate\":\"2024-01-15T11:00:00+00:00\"}";

        var result = _sut.DeserializeIssues(content);

        result.Should().HaveCount(2);
    }

    [Test]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new IssueBuilder()
            .WithId("xyz789")
            .WithTitle("Full Issue")
            .WithDescription("A detailed description")
            .WithStatus(IssueStatus.Complete)
            .WithType(IssueType.Feature)
            .WithLinkedPr(123)
            .WithLinkedIssues("abc123", "#456")
            .WithParentIssueIds("parent1")
            .WithPriority(2)
            .WithLastUpdate(new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero))
            .Build();

        var json = _sut.SerializeIssue(original);
        var deserialized = _sut.DeserializeIssue(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(original.Id);
        deserialized.Title.Should().Be(original.Title);
        deserialized.Description.Should().Be(original.Description);
        deserialized.Status.Should().Be(original.Status);
        deserialized.Type.Should().Be(original.Type);
        deserialized.LinkedPR.Should().Be(original.LinkedPR);
        deserialized.LinkedIssues.Should().BeEquivalentTo(original.LinkedIssues);
        deserialized.ParentIssues.Should().BeEquivalentTo(original.ParentIssues);
        deserialized.Priority.Should().Be(original.Priority);
        deserialized.LastUpdate.Should().Be(original.LastUpdate);
    }

    [Test]
    public void SerializeChange_ReturnsValidJson()
    {
        var change = new ChangeRecord
        {
            ChangeId = Guid.Parse("12345678-1234-1234-1234-123456789012"),
            IssueId = "abc123",
            Type = ChangeType.Created,
            ChangedBy = "Test User",
            ChangedAt = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero),
            PropertyChanges =
            [
                new PropertyChange
                {
                    PropertyName = "Title",
                    OldValue = null,
                    NewValue = "Test Issue",
                    Timestamp = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero)
                }
            ]
        };

        var result = _sut.SerializeChange(change);

        result.Should().Contain("\"changeId\"");
        result.Should().Contain("\"issueId\":\"abc123\"");
        result.Should().NotContain("\n");
    }

    [Test]
    public void DeserializeChanges_ParsesMultipleLines()
    {
        var content = "{\"changeId\":\"12345678-1234-1234-1234-123456789012\",\"issueId\":\"abc123\",\"type\":\"Created\",\"changedBy\":\"Test User\",\"changedAt\":\"2024-01-15T12:00:00+00:00\",\"propertyChanges\":[]}";

        var result = _sut.DeserializeChanges(content);

        result.Should().HaveCount(1);
        result[0].IssueId.Should().Be("abc123");
    }
}
