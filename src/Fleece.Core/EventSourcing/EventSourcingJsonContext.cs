using System.Text.Json;
using System.Text.Json.Serialization;
using Fleece.Core.EventSourcing.Events;
using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing;

/// <summary>
/// Source-generated JSON context for the event-sourced persistence layer.
/// Covers the lean <see cref="Issue"/> projection shape, the <see cref="IssueEvent"/>
/// hierarchy (polymorphic via the <c>kind</c> discriminator), and the active-change
/// pointer file.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Issue))]
[JsonSerializable(typeof(IReadOnlyList<Issue>))]
[JsonSerializable(typeof(ParentIssueRef))]
[JsonSerializable(typeof(IReadOnlyList<ParentIssueRef>))]
[JsonSerializable(typeof(Tombstone))]
[JsonSerializable(typeof(IReadOnlyList<Tombstone>))]
[JsonSerializable(typeof(IssueEvent))]
[JsonSerializable(typeof(MetaEvent))]
[JsonSerializable(typeof(CreateEvent))]
[JsonSerializable(typeof(SetEvent))]
[JsonSerializable(typeof(AddEvent))]
[JsonSerializable(typeof(RemoveEvent))]
[JsonSerializable(typeof(HardDeleteEvent))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(ActiveChangePointer))]
[JsonSerializable(typeof(ReplayCacheFile))]
public partial class EventSourcingJsonContext : JsonSerializerContext;
