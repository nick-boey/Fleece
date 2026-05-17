using System.Text.Json;
using System.Text.Json.Serialization;
using Fleece.Core.Models;

namespace Fleece.Core.Serialization;

/// <summary>
/// Custom JSON converter for ParentIssueRef that accepts both the current "lexOrder"
/// property name and the legacy "sortOrder" name written by older snapshots.
/// </summary>
public sealed class ParentIssueRefConverter : JsonConverter<ParentIssueRef>
{
    public override ParentIssueRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? parentIssue = null;
        string? sortOrder = null;
        bool active = true;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propName = reader.GetString();
            reader.Read();

            switch (propName)
            {
                case "parentIssue":
                    parentIssue = reader.GetString();
                    break;
                case "lexOrder":
                case "sortOrder": // legacy property name — written by pre-3.1 snapshots
                    sortOrder = reader.GetString();
                    break;
                case "active":
                    active = reader.GetBoolean();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (parentIssue is null)
        {
            throw new JsonException("Missing required property 'parentIssue' for ParentIssueRef.");
        }

        if (sortOrder is null)
        {
            throw new JsonException("Missing required property 'lexOrder' for ParentIssueRef.");
        }

        return new ParentIssueRef { ParentIssue = parentIssue, SortOrder = sortOrder, Active = active };
    }

    public override void Write(Utf8JsonWriter writer, ParentIssueRef value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("parentIssue", value.ParentIssue);
        writer.WriteString("lexOrder", value.SortOrder);
        writer.WriteBoolean("active", value.Active);
        writer.WriteEndObject();
    }
}
