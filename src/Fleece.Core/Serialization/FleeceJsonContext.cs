using System.Text.Json;
using System.Text.Json.Serialization;
using Fleece.Core.Models;

namespace Fleece.Core.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    Converters = [typeof(IssueStatusConverter)])]
[JsonSerializable(typeof(Issue))]
[JsonSerializable(typeof(IssueDto))]
[JsonSerializable(typeof(PropertyChange))]
[JsonSerializable(typeof(MergeResult))]
[JsonSerializable(typeof(Question))]
[JsonSerializable(typeof(ParentIssueRef))]
[JsonSerializable(typeof(IReadOnlyList<Issue>))]
[JsonSerializable(typeof(IReadOnlyList<IssueDto>))]
[JsonSerializable(typeof(IReadOnlyList<PropertyChange>))]
[JsonSerializable(typeof(IReadOnlyList<Question>))]
[JsonSerializable(typeof(IReadOnlyList<ParentIssueRef>))]
[JsonSerializable(typeof(Tombstone))]
[JsonSerializable(typeof(IReadOnlyList<Tombstone>))]
[JsonSerializable(typeof(CleanResult))]
[JsonSerializable(typeof(StrippedReference))]
[JsonSerializable(typeof(IReadOnlyList<StrippedReference>))]
[JsonSerializable(typeof(IssueSummaryDto))]
[JsonSerializable(typeof(ParentContextDto))]
[JsonSerializable(typeof(IssueShowDto))]
[JsonSerializable(typeof(IReadOnlyList<IssueSummaryDto>))]
[JsonSerializable(typeof(IReadOnlyList<ParentContextDto>))]
public partial class FleeceJsonContext : JsonSerializerContext
{
}
