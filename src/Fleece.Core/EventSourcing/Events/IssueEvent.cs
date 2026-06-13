using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fleece.Core.EventSourcing.Events;

/// <summary>
/// Base type for events appended to a per-issue log at <c>.fleece/issues/{id}.jsonl</c>.
/// </summary>
/// <remarks>
/// Discriminated by the <c>kind</c> JSON property via <see cref="JsonPolymorphicAttribute"/>
/// so System.Text.Json can read/write the hierarchy under AOT.
/// </remarks>
[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
    IgnoreUnrecognizedTypeDiscriminators = false)]
[JsonDerivedType(typeof(CreateEvent), "create")]
[JsonDerivedType(typeof(SetEvent), "set")]
[JsonDerivedType(typeof(AddEvent), "add")]
[JsonDerivedType(typeof(RemoveEvent), "remove")]
public abstract record IssueEvent
{
    /// <summary>The discriminator string. Available at runtime; not emitted in JSON (handled by polymorphism).</summary>
    [JsonIgnore]
    public abstract string Kind { get; }
}

/// <summary>
/// Inserts a new issue. Subsequent events on the same issue mutate the inserted state.
/// </summary>
public sealed record CreateEvent : IssueEvent
{
    [JsonIgnore]
    public override string Kind => "create";

    public required DateTimeOffset At { get; init; }

    public string? By { get; init; }

    public required string IssueId { get; init; }

    /// <summary>
    /// Initial property bag for the new issue (title, type, status, createdAt, etc.).
    /// Held as <see cref="JsonElement"/> so unknown properties round-trip cleanly.
    /// </summary>
    public required JsonElement Data { get; init; }
}

/// <summary>Overwrites a scalar property. Null is a legal value (clears the property).</summary>
public sealed record SetEvent : IssueEvent
{
    [JsonIgnore]
    public override string Kind => "set";

    public required DateTimeOffset At { get; init; }

    public string? By { get; init; }

    public required string IssueId { get; init; }

    public required string Property { get; init; }

    public JsonElement Value { get; init; }
}

/// <summary>Appends an element to an array property. Idempotent: re-adding the same value is a no-op.</summary>
public sealed record AddEvent : IssueEvent
{
    [JsonIgnore]
    public override string Kind => "add";

    public required DateTimeOffset At { get; init; }

    public string? By { get; init; }

    public required string IssueId { get; init; }

    public required string Property { get; init; }

    public required JsonElement Value { get; init; }
}

/// <summary>
/// Removes an element from an array property. Idempotent: removing an absent value is a no-op.
/// For structured elements (e.g. <c>parentIssues</c>) the natural key on the value object is matched.
/// </summary>
public sealed record RemoveEvent : IssueEvent
{
    [JsonIgnore]
    public override string Kind => "remove";

    public required DateTimeOffset At { get; init; }

    public string? By { get; init; }

    public required string IssueId { get; init; }

    public required string Property { get; init; }

    public required JsonElement Value { get; init; }
}
