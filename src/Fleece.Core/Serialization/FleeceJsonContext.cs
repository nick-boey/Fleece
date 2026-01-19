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
[JsonSerializable(typeof(ConflictRecord))]
[JsonSerializable(typeof(PropertyConflict))]
[JsonSerializable(typeof(MergeResult))]
[JsonSerializable(typeof(IReadOnlyList<Issue>))]
[JsonSerializable(typeof(IReadOnlyList<ConflictRecord>))]
[JsonSerializable(typeof(IReadOnlyList<PropertyConflict>))]
public partial class FleeceJsonContext : JsonSerializerContext
{
}
