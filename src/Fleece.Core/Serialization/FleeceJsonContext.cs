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
[JsonSerializable(typeof(FleeceSettings))]
[JsonSerializable(typeof(EffectiveSettings))]
[JsonSerializable(typeof(SettingsSources))]
[JsonSerializable(typeof(IssueSyncDto))]
[JsonSerializable(typeof(IReadOnlyList<IssueSyncDto>))]
public partial class FleeceJsonContext : JsonSerializerContext
{
}

/// <summary>
/// JSON context for settings files with pretty-printing enabled.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(FleeceSettings))]
public partial class FleeceSettingsWriteContext : JsonSerializerContext
{
}
