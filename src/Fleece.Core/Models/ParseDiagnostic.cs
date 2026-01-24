namespace Fleece.Core.Models;

/// <summary>
/// Diagnostic information from parsing a single JSONL file.
/// </summary>
public sealed record ParseDiagnostic
{
    public required string FilePath { get; init; }
    public required int TotalRows { get; init; }
    public required int ParsedRows { get; init; }
    public required int FailedRows { get; init; }
    public IReadOnlySet<string> UnknownProperties { get; init; } = new HashSet<string>();
    public IReadOnlyList<string> ParseErrors { get; init; } = [];

    /// <summary>
    /// True if there are any issues (unknown properties or parse errors).
    /// </summary>
    public bool HasIssues => UnknownProperties.Count > 0 || ParseErrors.Count > 0;

    /// <summary>
    /// Number of rows that were skipped (failed + empty).
    /// </summary>
    public int SkippedRows => TotalRows - ParsedRows;
}
