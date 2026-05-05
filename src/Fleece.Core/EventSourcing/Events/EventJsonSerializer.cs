using System.Text.Json;

namespace Fleece.Core.EventSourcing.Events;

/// <summary>
/// Serialize/parse helpers for change-file event lines. Wraps the source-generated
/// <see cref="EventSourcingJsonContext"/> with file/line diagnostic context for parse errors.
/// </summary>
public static class EventJsonSerializer
{
    /// <summary>Serializes a single event as one line of JSON (no trailing newline).</summary>
    public static string Serialize(IssueEvent issueEvent) =>
        JsonSerializer.Serialize(issueEvent, EventSourcingJsonContext.Default.IssueEvent);

    /// <summary>
    /// Parses a single change-file line. <paramref name="filePath"/> and <paramref name="lineNumber"/>
    /// are used for diagnostic context if parsing fails. Throws <see cref="UnknownEventKindException"/>
    /// for unrecognized <c>kind</c> values and <see cref="EventParseException"/> for any other JSON error.
    /// </summary>
    public static IssueEvent ParseLine(string line, string filePath, int lineNumber)
    {
        try
        {
            var evt = JsonSerializer.Deserialize(line, EventSourcingJsonContext.Default.IssueEvent)
                ?? throw new EventParseException(filePath, lineNumber, "Deserialized event was null.");
            return evt;
        }
        catch (NotSupportedException ex)
        {
            // Polymorphic deserialization throws NotSupportedException on an unrecognized
            // discriminator. Surface it with file/line context and the offending kind.
            var kind = TryExtractKind(line);
            throw new UnknownEventKindException(filePath, lineNumber, kind ?? "<unparseable>", ex);
        }
        catch (JsonException ex)
        {
            // System.Text.Json may surface unknown-discriminator errors as JsonException too.
            // Distinguish by inspecting the message — if it mentions the discriminator,
            // treat as UnknownEventKindException; otherwise it's a general parse error.
            if (ex.Message.Contains("discriminator", StringComparison.OrdinalIgnoreCase))
            {
                var kind = TryExtractKind(line);
                throw new UnknownEventKindException(filePath, lineNumber, kind ?? "<unparseable>", ex);
            }
            throw new EventParseException(filePath, lineNumber, ex.Message, ex);
        }
    }

    private static string? TryExtractKind(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("kind", out var kindElem) &&
                kindElem.ValueKind == JsonValueKind.String)
            {
                return kindElem.GetString();
            }
        }
        catch
        {
            // ignore — we're already in an error path
        }
        return null;
    }
}

/// <summary>
/// Thrown when a change file contains an event with an unrecognized <c>kind</c> discriminator.
/// </summary>
public sealed class UnknownEventKindException : Exception
{
    public string FilePath { get; }
    public int LineNumber { get; }
    public string Kind { get; }

    public UnknownEventKindException(string filePath, int lineNumber, string kind, Exception? inner = null)
        : base($"Unknown event kind '{kind}' in {filePath} at line {lineNumber}.", inner)
    {
        FilePath = filePath;
        LineNumber = lineNumber;
        Kind = kind;
    }
}

/// <summary>Thrown when a change-file line cannot be parsed as JSON.</summary>
public sealed class EventParseException : Exception
{
    public string FilePath { get; }
    public int LineNumber { get; }

    public EventParseException(string filePath, int lineNumber, string message, Exception? inner = null)
        : base($"Failed to parse event at {filePath}:{lineNumber}: {message}", inner)
    {
        FilePath = filePath;
        LineNumber = lineNumber;
    }
}
