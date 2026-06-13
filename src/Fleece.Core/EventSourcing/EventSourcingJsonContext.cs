using System.Text.Json;
using System.Text.Json.Serialization;
using Fleece.Core.EventSourcing.Events;
using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing;

/// <summary>
/// Source-generated JSON context for the event-sourced persistence layer.
/// Covers the lean <see cref="Issue"/> projection shape and the <see cref="IssueEvent"/>
/// hierarchy (polymorphic via the <c>kind</c> discriminator) appended to per-issue logs.
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
[JsonSerializable(typeof(CreateEvent))]
[JsonSerializable(typeof(SetEvent))]
[JsonSerializable(typeof(AddEvent))]
[JsonSerializable(typeof(RemoveEvent))]
[JsonSerializable(typeof(JsonElement))]
public partial class EventSourcingJsonContext : JsonSerializerContext;
