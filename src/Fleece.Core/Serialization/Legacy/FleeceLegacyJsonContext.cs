using System.Text.Json.Serialization;
using Fleece.Core.Models;
using Fleece.Core.Models.Legacy;

namespace Fleece.Core.Serialization.Legacy;

/// <summary>
/// Source-generated JSON context for the legacy issue and parent-ref shapes.
/// Used only by the one-shot migrate-events path; production reads/writes go
/// through <see cref="FleeceJsonContext"/>.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    Converters = [typeof(IssueStatusConverter)])]
[JsonSerializable(typeof(LegacyIssue))]
[JsonSerializable(typeof(LegacyParentIssueRef))]
[JsonSerializable(typeof(IReadOnlyList<LegacyIssue>))]
[JsonSerializable(typeof(IReadOnlyList<LegacyParentIssueRef>))]
[JsonSerializable(typeof(Tombstone))]
[JsonSerializable(typeof(IReadOnlyList<Tombstone>))]
public partial class FleeceLegacyJsonContext : JsonSerializerContext
{
}
