namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// One-shot migration from the legacy hashed file layout
/// (<c>.fleece/issues_{hash}.jsonl</c> + <c>.fleece/tombstones_{hash}.jsonl</c>) to the
/// event-sourced layout (<c>.fleece/issues.jsonl</c> + <c>.fleece/tombstones.jsonl</c>
/// + <c>.fleece/changes/</c>).
/// </summary>
public interface IEventMigrationService
{
    /// <summary>
    /// Returns true when there is at least one legacy hashed file present in <c>.fleece/</c>.
    /// </summary>
    Task<bool> IsMigrationNeededAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs the migration. Idempotent: when <see cref="IsMigrationNeededAsync"/> returns
    /// false, this returns a result with <see cref="EventMigrationResult.WasMigrationNeeded"/>
    /// equal to false and writes nothing.
    /// </summary>
    Task<EventMigrationResult> MigrateAsync(string? mergedBy = null, CancellationToken cancellationToken = default);
}

/// <summary>Summary of a migration run.</summary>
public sealed record EventMigrationResult
{
    public required bool WasMigrationNeeded { get; init; }
    public required int LegacyIssueFilesConsumed { get; init; }
    public required int LegacyTombstoneFilesConsumed { get; init; }
    public required int IssuesWritten { get; init; }
    public required int TombstonesWritten { get; init; }
    public required IReadOnlyList<string> GitignoreEntriesAdded { get; init; }
}
