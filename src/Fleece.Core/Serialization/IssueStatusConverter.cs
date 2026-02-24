using System.Text.Json;
using System.Text.Json.Serialization;
using Fleece.Core.Models;

namespace Fleece.Core.Serialization;

/// <summary>
/// Custom JSON converter for IssueStatus that handles migration from old status values
/// (Idea, Spec, Next) to the new Open status.
/// </summary>
public sealed class IssueStatusConverter : JsonConverter<IssueStatus>
{
    public override IssueStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            return IssueStatus.Open;
        }

        // Handle old status values by mapping them to Open
        return value.ToLowerInvariant() switch
        {
            "idea" => IssueStatus.Open,
            "spec" => IssueStatus.Open,
            "next" => IssueStatus.Open,
            "draft" => IssueStatus.Draft,
            "open" => IssueStatus.Open,
            "progress" => IssueStatus.Progress,
            "review" => IssueStatus.Review,
            "complete" => IssueStatus.Complete,
            "archived" => IssueStatus.Archived,
            "closed" => IssueStatus.Closed,
            "deleted" => IssueStatus.Deleted,
            _ => throw new JsonException($"Unknown IssueStatus value: {value}")
        };
    }

    public override void Write(Utf8JsonWriter writer, IssueStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
