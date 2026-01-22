namespace Fleece.Core.Models;

/// <summary>
/// Represents a question asked about an issue with optional answer.
/// </summary>
public sealed record Question
{
    /// <summary>
    /// Unique identifier for the question (e.g., "Q12345").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The question text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The answer text, if provided.
    /// </summary>
    public string? Answer { get; init; }

    /// <summary>
    /// When the question was asked.
    /// </summary>
    public required DateTimeOffset AskedAt { get; init; }

    /// <summary>
    /// Who asked the question.
    /// </summary>
    public string? AskedBy { get; init; }

    /// <summary>
    /// When the question was answered.
    /// </summary>
    public DateTimeOffset? AnsweredAt { get; init; }

    /// <summary>
    /// Who answered the question.
    /// </summary>
    public string? AnsweredBy { get; init; }
}
