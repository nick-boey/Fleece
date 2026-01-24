namespace Fleece.Core.Models;

/// <summary>
/// Result of loading issues with diagnostic information.
/// </summary>
public sealed record LoadIssuesResult
{
    public IReadOnlyList<Issue> Issues { get; init; } = [];
    public IReadOnlyList<ParseDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// True if any diagnostic has issues (unknown properties or parse errors).
    /// </summary>
    public bool HasWarnings => Diagnostics.Any(d => d.HasIssues);
}
