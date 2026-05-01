namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Schema-migration service for <c>fleece migrate</c>. Today it migrates the legacy
/// hashed file layout (<c>.fleece/issues_{hash}.jsonl</c> +
/// <c>.fleece/tombstones_{hash}.jsonl</c>) into the event-sourced layout
/// (<c>.fleece/issues.jsonl</c> + <c>.fleece/tombstones.jsonl</c> +
/// <c>.fleece/changes/</c>). Future schema migrations on the lean shape extend this
/// pipeline rather than introducing new commands.
/// </summary>
public interface IMigrationService
{
    /// <summary>
    /// Returns true when there is at least one legacy hashed file present in <c>.fleece/</c>.
    /// </summary>
    Task<bool> IsMigrationNeededAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs the migration. Idempotent: when <see cref="IsMigrationNeededAsync"/> returns
    /// false, this returns a result with <see cref="MigrationResult.WasMigrationNeeded"/>
    /// equal to false and writes nothing.
    /// </summary>
    Task<MigrationResult> MigrateAsync(string? mergedBy = null, CancellationToken cancellationToken = default);
}

/// <summary>Summary of a migration run.</summary>
public sealed record MigrationResult
{
    public required bool WasMigrationNeeded { get; init; }
    public required int LegacyIssueFilesConsumed { get; init; }
    public required int LegacyTombstoneFilesConsumed { get; init; }
    public required int IssuesWritten { get; init; }
    public required int TombstonesWritten { get; init; }
    public required IReadOnlyList<string> GitignoreEntriesAdded { get; init; }
}
