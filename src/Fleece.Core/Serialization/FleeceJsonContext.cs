using System.Text.Json;
using System.Text.Json.Serialization;
using Fleece.Core.Models;

namespace Fleece.Core.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Issue))]
[JsonSerializable(typeof(IssueDto))]
[JsonSerializable(typeof(ChangeRecord))]
[JsonSerializable(typeof(PropertyChange))]
[JsonSerializable(typeof(ChangeType))]
[JsonSerializable(typeof(MergeResult))]
[JsonSerializable(typeof(Question))]
[JsonSerializable(typeof(IReadOnlyList<Issue>))]
[JsonSerializable(typeof(IReadOnlyList<IssueDto>))]
[JsonSerializable(typeof(IReadOnlyList<ChangeRecord>))]
[JsonSerializable(typeof(IReadOnlyList<PropertyChange>))]
[JsonSerializable(typeof(IReadOnlyList<Question>))]
public partial class FleeceJsonContext : JsonSerializerContext
{
}
