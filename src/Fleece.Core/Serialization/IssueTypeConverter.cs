using System.Text.Json;
using System.Text.Json.Serialization;
using Fleece.Core.Models;

namespace Fleece.Core.Serialization;

/// <summary>
/// Custom JSON converter for IssueType that maps removed legacy type values
/// (notably <c>Idea</c>) onto a surviving type so the legacy migration pipeline can
/// bring old hashed-file repositories forward without failing on dropped enum members.
/// </summary>
public sealed class IssueTypeConverter : JsonConverter<IssueType>
{
    public override IssueType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            return IssueType.Task;
        }

        return value.ToLowerInvariant() switch
        {
            // Removed in v4: planning "ideas" become plain tasks on bring-forward.
            "idea" => IssueType.Task,
            "task" => IssueType.Task,
            "bug" => IssueType.Bug,
            "chore" => IssueType.Chore,
            "feature" => IssueType.Feature,
            "verify" => IssueType.Verify,
            _ => throw new JsonException($"Unknown IssueType value: {value}")
        };
    }

    public override void Write(Utf8JsonWriter writer, IssueType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
