using Fleece.Core.EventSourcing.Events;
using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Read/write surface over the per-issue append-only logs in <c>.fleece/issues/</c>.
/// Reads return the in-memory state computed by replaying every log independently.
/// Writes append events to the target issues' own logs; deletes remove a log file.
/// </summary>
public interface IEventSourcedStorageService
{
    /// <summary>Returns the current in-memory state, computed by replaying every per-issue log.</summary>
    Task<IReadOnlyDictionary<string, Issue>> GetIssuesAsync(CancellationToken cancellationToken = default);

    /// <summary>Appends one or more events to their target issues' logs (grouped by issue id).</summary>
    Task AppendEventsAsync(IReadOnlyList<IssueEvent> events, CancellationToken cancellationToken = default);

    /// <summary>Removes an issue by deleting its <c>.fleece/issues/{id}.jsonl</c> log file.</summary>
    Task DeleteIssueAsync(string issueId, CancellationToken cancellationToken = default);
}
