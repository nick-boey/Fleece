namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Writes <c>change_{guid}.jsonl</c> "merge marker" files that linearise parallel
/// change-file chains at the moment a git merge happens. Intended to be invoked
/// from <c>pre-merge-commit</c> / <c>pre-commit</c> hooks installed by <c>fleece install</c>,
/// but also runnable manually for post-hoc reconciliation.
/// </summary>
public interface ILinkService
{
    /// <summary>
    /// If a merge is in progress (<c>.git/MERGE_HEAD</c> exists), writes a marker change
    /// file whose meta event's <c>follows</c> lists every DAG leaf in <c>.fleece/changes/</c>
    /// and stages the new file via <c>git add</c>. If no merge is in progress, this is a no-op.
    /// </summary>
    Task<LinkResult> CreateMergeMarkerAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of <see cref="ILinkService.CreateMergeMarkerAsync"/>. <see cref="MarkerCreated"/>
/// is false when no merge was in progress (the command is a safe no-op outside a merge).
/// </summary>
public sealed record LinkResult(
    bool MarkerCreated,
    string? MarkerGuid,
    string? MarkerPath,
    IReadOnlyList<string> Parents,
    string Message);
