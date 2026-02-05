namespace Fleece.Core.Models;

/// <summary>
/// Result of migrating issues to the new property timestamp format.
/// </summary>
public sealed record MigrationResult
{
    public required int TotalIssues { get; init; }
    public required int MigratedIssues { get; init; }
    public required int AlreadyMigratedIssues { get; init; }
    public bool WasMigrationNeeded => MigratedIssues > 0 || UnknownPropertiesDeleted.Count > 0;

    /// <summary>
    /// Set of unknown property names that were removed from the JSONL files.
    /// </summary>
    public IReadOnlySet<string> UnknownPropertiesDeleted { get; init; } = new HashSet<string>();
}
