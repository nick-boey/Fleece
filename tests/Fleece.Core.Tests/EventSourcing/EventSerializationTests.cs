using System.Text.Json;
using Fleece.Core.EventSourcing.Events;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.EventSourcing;

[TestFixture]
public sealed class EventSerializationTests
{
    [Test]
    public void MetaEvent_Roundtrips()
    {
        var evt = new MetaEvent { Follows = "abc-123" };
        var json = EventJsonSerializer.Serialize(evt);
        json.Should().Contain("\"kind\":\"meta\"").And.Contain("\"follows\":\"abc-123\"");

        var parsed = EventJsonSerializer.ParseLine(json, "x.jsonl", 1);
        parsed.Should().BeEquivalentTo(evt);
    }

    [Test]
    public void MetaEvent_NullFollows_Roundtrips()
    {
        var evt = new MetaEvent { Follows = null };
        var json = EventJsonSerializer.Serialize(evt);
        // null is omitted on write per DefaultIgnoreCondition.WhenWritingNull;
        // round-trip still produces null.
        json.Should().Contain("\"kind\":\"meta\"");

        var parsed = (MetaEvent)EventJsonSerializer.ParseLine(json, "x.jsonl", 1);
        parsed.Follows.Should().BeNull();
    }

    [Test]
    public void CreateEvent_Roundtrips()
    {
        var dataJson = JsonDocument.Parse("""{"title":"Foo","type":"task","status":"open"}""").RootElement;
        var evt = new CreateEvent
        {
            At = DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
            By = "alice",
            IssueId = "abc123",
            Data = dataJson,
        };
        var json = EventJsonSerializer.Serialize(evt);
        json.Should().Contain("\"kind\":\"create\"").And.Contain("\"issueId\":\"abc123\"").And.Contain("\"by\":\"alice\"");

        var parsed = (CreateEvent)EventJsonSerializer.ParseLine(json, "x.jsonl", 1);
        parsed.IssueId.Should().Be("abc123");
        parsed.By.Should().Be("alice");
        parsed.At.Should().Be(evt.At);
        parsed.Data.GetProperty("title").GetString().Should().Be("Foo");
    }

    [Test]
    public void SetEvent_NullValue_Roundtrips()
    {
        var nullValue = JsonDocument.Parse("null").RootElement;
        var evt = new SetEvent
        {
            At = DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
            By = null,
            IssueId = "abc123",
            Property = "description",
            Value = nullValue,
        };
        var json = EventJsonSerializer.Serialize(evt);
        json.Should().Contain("\"kind\":\"set\"").And.Contain("\"value\":null");

        var parsed = (SetEvent)EventJsonSerializer.ParseLine(json, "x.jsonl", 1);
        parsed.Property.Should().Be("description");
        parsed.Value.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Test]
    public void AddEvent_ParentIssueRef_Roundtrips()
    {
        var refJson = JsonDocument.Parse("""{"parentIssue":"P1","lexOrder":"aaa","active":true}""").RootElement;
        var evt = new AddEvent
        {
            At = DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
            IssueId = "child",
            Property = "parentIssues",
            Value = refJson,
        };
        var json = EventJsonSerializer.Serialize(evt);
        json.Should().Contain("\"kind\":\"add\"");

        var parsed = (AddEvent)EventJsonSerializer.ParseLine(json, "x.jsonl", 1);
        parsed.Value.GetProperty("parentIssue").GetString().Should().Be("P1");
        parsed.Value.GetProperty("lexOrder").GetString().Should().Be("aaa");
        parsed.Value.GetProperty("active").GetBoolean().Should().BeTrue();
    }

    [Test]
    public void RemoveEvent_KeyOnly_Roundtrips()
    {
        var keyOnly = JsonDocument.Parse("""{"parentIssue":"P1"}""").RootElement;
        var evt = new RemoveEvent
        {
            At = DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
            IssueId = "child",
            Property = "parentIssues",
            Value = keyOnly,
        };
        var json = EventJsonSerializer.Serialize(evt);
        var parsed = (RemoveEvent)EventJsonSerializer.ParseLine(json, "x.jsonl", 1);
        parsed.Value.GetProperty("parentIssue").GetString().Should().Be("P1");
        parsed.Value.TryGetProperty("lexOrder", out _).Should().BeFalse();
    }

    [Test]
    public void HardDeleteEvent_Roundtrips()
    {
        var evt = new HardDeleteEvent
        {
            At = DateTimeOffset.Parse("2026-04-30T10:00:00Z"),
            By = "bot",
            IssueId = "xyz",
        };
        var json = EventJsonSerializer.Serialize(evt);
        json.Should().Contain("\"kind\":\"hard-delete\"").And.Contain("\"issueId\":\"xyz\"");

        var parsed = (HardDeleteEvent)EventJsonSerializer.ParseLine(json, "x.jsonl", 1);
        parsed.IssueId.Should().Be("xyz");
        parsed.By.Should().Be("bot");
    }

    [Test]
    public void UnknownKind_Throws_WithFileAndLineContext()
    {
        var line = """{"kind":"reticulate-splines","at":"2026-04-30T10:00:00Z","issueId":"abc"}""";
        var act = () => EventJsonSerializer.ParseLine(line, "changes/change_xxx.jsonl", 7);

        act.Should()
            .Throw<UnknownEventKindException>()
            .Where(e => e.FilePath == "changes/change_xxx.jsonl"
                && e.LineNumber == 7
                && e.Kind == "reticulate-splines");
    }

    [Test]
    public void MalformedJson_Throws_EventParseException_WithLine()
    {
        var line = "not json at all";
        var act = () => EventJsonSerializer.ParseLine(line, "f.jsonl", 3);
        act.Should().Throw<EventParseException>()
            .Where(e => e.FilePath == "f.jsonl" && e.LineNumber == 3);
    }
}
