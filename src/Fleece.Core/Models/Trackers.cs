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
    /// Used at write time (<c>config --set</c>, <c>install --tracker</c>) where invalid input must fail loudly.
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

    /// <summary>
    /// Normalizes a tracker value case-insensitively for READ paths (settings merge). Returns the
    /// canonical lowercase tracker for a recognized value, or <c>null</c> for an empty/whitespace or
    /// unrecognized value — so a hand-edited <c>.fleece/settings.json</c> carrying <c>"LINEAR"</c> is
    /// honoured as <c>linear</c>, while a garbage value (<c>"jira"</c>) is treated as unset rather than
    /// silently routing to a different tracker than the effective settings report.
    /// </summary>
    public static string? TryNormalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized == GitHub || normalized == Linear ? normalized : null;
    }
}
