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
[JsonSerializable(typeof(ChangeRecord))]
[JsonSerializable(typeof(PropertyChange))]
[JsonSerializable(typeof(ChangeType))]
[JsonSerializable(typeof(MergeResult))]
[JsonSerializable(typeof(IReadOnlyList<Issue>))]
[JsonSerializable(typeof(IReadOnlyList<ChangeRecord>))]
[JsonSerializable(typeof(IReadOnlyList<PropertyChange>))]
public partial class FleeceJsonContext : JsonSerializerContext
{
}
