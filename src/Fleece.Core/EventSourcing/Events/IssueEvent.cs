using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fleece.Core.EventSourcing.Events;

/// <summary>
/// Base type for events appended to <c>.fleece/changes/change_{guid}.jsonl</c> files.
/// </summary>
/// <remarks>
/// Discriminated by the <c>kind</c> JSON property via <see cref="JsonPolymorphicAttribute"/>
/// so System.Text.Json can read/write the hierarchy under AOT.
/// </remarks>
[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
    IgnoreUnrecognizedTypeDiscriminators = false)]
[JsonDerivedType(typeof(MetaEvent), "meta")]
[JsonDerivedType(typeof(CreateEvent), "create")]
[JsonDerivedType(typeof(SetEvent), "set")]
[JsonDerivedType(typeof(AddEvent), "add")]
[JsonDerivedType(typeof(RemoveEvent), "remove")]
[JsonDerivedType(typeof(HardDeleteEvent), "hard-delete")]
public abstract record IssueEvent
{
    /// <summary>The discriminator string. Available at runtime; not emitted in JSON (handled by polymorphism).</summary>
    [JsonIgnore]
    public abstract string Kind { get; }
}

/// <summary>
/// First line of every change file. Carries the <c>follows</c> pointer(s) identifying
/// the predecessor change file(s) this one continues. Empty = the file is a DAG root;
/// one entry = a single-parent chain (most common); two or more entries = a merge
/// marker linking parallel chains.
/// </summary>
/// <remarks>
/// On the wire, <c>follows</c> may be <c>null</c>, a scalar string, or an array of strings.
/// We hold it as a <see cref="JsonElement"/> on the JSON-facing property so STJ source-gen
/// reads/writes all three shapes losslessly; the typed <see cref="Follows"/> property
/// converts to/from <see cref="IReadOnlyList{T}"/> for callers.
/// </remarks>
public sealed record MetaEvent : IssueEvent
{
    [JsonIgnore]
    public override string Kind => "meta";

    /// <summary>
    /// Raw wire-format <c>follows</c> value. Null = root; string = single parent;
    /// array = multi-parent marker. Exposed for serialisation only — callers should
    /// use <see cref="Follows"/>.
    /// </summary>
    [JsonPropertyName("follows")]
    public JsonElement? FollowsRaw { get; init; }

    /// <summary>
    /// Typed view of <see cref="FollowsRaw"/>. Empty list = root; one entry = single
    /// parent; two or more entries = merge marker. Assigning here updates the wire form.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> Follows
    {
        get
        {
            if (FollowsRaw is null)
            {
                return Array.Empty<string>();
            }
            var elem = FollowsRaw.Value;
            return elem.ValueKind switch
            {
                JsonValueKind.Null => Array.Empty<string>(),
                JsonValueKind.String => new[] { elem.GetString()! },
                JsonValueKind.Array => elem.EnumerateArray().Select(e =>
                    e.ValueKind == JsonValueKind.String
                        ? e.GetString()!
                        : throw new InvalidOperationException("follows array must contain only strings.")).ToArray(),
                _ => throw new InvalidOperationException(
                    $"follows must be null, a string, or an array of strings; got {elem.ValueKind}."),
            };
        }
        init
        {
            FollowsRaw = BuildFollowsElement(value);
        }
    }

    /// <summary>
    /// Builds a <see cref="JsonElement"/> for the wire representation of <paramref name="follows"/>
    /// without going through the reflection-based serializer (AOT-safe).
    /// </summary>
    private static JsonElement BuildFollowsElement(IReadOnlyList<string>? follows)
    {
        if (follows is null || follows.Count == 0)
        {
            return JsonDocument.Parse("null").RootElement;
        }

        using var ms = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            if (follows.Count == 1)
            {
                writer.WriteStringValue(follows[0]);
            }
            else
            {
                writer.WriteStartArray();
                foreach (var item in follows)
                {
                    writer.WriteStringValue(item);
                }
                writer.WriteEndArray();
            }
        }
        ms.Position = 0;
        return JsonDocument.Parse(ms).RootElement;
    }
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

/// <summary>Drops the issue from the projected state. Tombstones are written by the projection step.</summary>
public sealed record HardDeleteEvent : IssueEvent
{
    [JsonIgnore]
    public override string Kind => "hard-delete";

    public required DateTimeOffset At { get; init; }

    public string? By { get; init; }

    public required string IssueId { get; init; }
}
