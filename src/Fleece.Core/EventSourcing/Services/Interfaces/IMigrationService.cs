namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Schema-migration service for <c>fleece migrate</c>. The one-time bring-forward of a legacy
/// repository into the v4 event-sourced layout (one append-only per-issue log at
/// <c>.fleece/issues/&lt;id&gt;.jsonl</c>). It auto-detects and converts whichever legacy layout
/// is present: the pre-event-sourced hashed-file layout (<c>.fleece/issues_{hash}.jsonl</c> +
/// <c>.fleece/tombstones_{hash}.jsonl</c>) and the durable snapshot layout
/// (<c>.fleece/issues.jsonl</c> + <c>.fleece/changes/</c>). v4 keeps no tombstone sidecar and no
/// <c>.fleece/changes/</c> directory. Only the hashed-file layout is auto-migrated by the command
/// interceptor; the durable snapshot is converted only on an explicit <c>fleece migrate</c> (the
/// interceptor merely warns and routes the user to <c>fleece prime v4-migration</c>).
/// </summary>
public interface IMigrationService
{
    /// <summary>
    /// Returns true when there is at least one legacy hashed file present in <c>.fleece/</c>.
    /// </summary>
    Task<bool> IsMigrationNeededAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs the migration, auto-detecting and converting whichever legacy layout is present:
    /// the hashed-file layout (Layout A, also reported by <see cref="IsMigrationNeededAsync"/>) and
    /// the durable snapshot layout (Layout B — <c>.fleece/issues.jsonl</c> + <c>.fleece/changes/</c>).
    /// <paramref name="convertDurableLayout"/> gates Layout B: the explicit <c>fleece migrate</c>
    /// command leaves it <c>true</c>, while the command interceptor's automatic migration passes
    /// <c>false</c> so it never converts the durable layout (even in a repository that also has
    /// hashed files). Idempotent: when no convertible layout is present, this returns a result with
    /// <see cref="MigrationResult.WasMigrationNeeded"/> equal to false and writes nothing.
    /// </summary>
    Task<MigrationResult> MigrateAsync(
        string? mergedBy = null,
        bool convertDurableLayout = true,
        CancellationToken cancellationToken = default);
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
