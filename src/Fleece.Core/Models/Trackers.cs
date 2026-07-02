namespace Fleece.Core.Models;

/// <summary>
/// The set of durable issue trackers a repository can escalate promoted work into. Exactly one is
/// active per repository (the <c>tracker</c> setting), defaulting to <see cref="GitHub"/>.
/// </summary>
public static class Trackers
{
    /// <summary>GitHub Issues — the default durable tracker (native CLI integration).</summary>
    public const string GitHub = "github";

    /// <summary>Linear — an agent-realized tracker; the CLI performs only local operations.</summary>
    public const string Linear = "linear";

    /// <summary>The value used when no tracker is configured.</summary>
    public const string Default = GitHub;

    /// <summary>
    /// Normalizes and validates a tracker value. Returns <c>null</c> for an empty value (clears the
    /// setting); throws <see cref="System.ArgumentException"/> for any value that is not a known tracker.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized != GitHub && normalized != Linear)
        {
            throw new ArgumentException($"Invalid tracker: {value}. Valid trackers are: {GitHub}, {Linear}");
        }

        return normalized;
    }
}
