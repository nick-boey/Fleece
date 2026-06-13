using Fleece.Core.EventSourcing.Events;

namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Read/write surface over the per-issue append-only logs in <c>.fleece/issues/</c>.
/// Each issue owns one file, <c>.fleece/issues/{id}.jsonl</c>, whose line order is
/// authoritative. There is no cross-file ordering, no <c>follows</c> pointer, and no
/// <c>meta</c> event.
/// </summary>
public interface IEventStore
{
    /// <summary>Lists every per-issue log file currently present in <c>.fleece/issues/</c> (full paths).</summary>
    Task<IReadOnlyList<string>> GetAllIssueLogPathsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and parses every event in a per-issue log file, in append (line) order.
    /// </summary>
    Task<IReadOnlyList<IssueEvent>> ReadIssueLogAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends one or more events to their target issues' logs. Events are grouped by
    /// <c>IssueId</c> and each group is appended to that issue's <c>{id}.jsonl</c> file
    /// (created on the first <c>create</c> event).
    /// </summary>
    Task AppendEventsAsync(IReadOnlyList<IssueEvent> events, CancellationToken cancellationToken = default);

    /// <summary>Deletes the issue's log file <c>.fleece/issues/{id}.jsonl</c> if it exists.</summary>
    Task DeleteIssueLogAsync(string issueId, CancellationToken cancellationToken = default);
}
