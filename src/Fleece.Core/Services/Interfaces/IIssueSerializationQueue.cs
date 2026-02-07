namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Defines the contract for queuing issue write operations for asynchronous persistence.
/// </summary>
public interface IIssueSerializationQueue : IDisposable
{
    /// <summary>
    /// Enqueues a write operation for asynchronous persistence to disk.
    /// </summary>
    ValueTask EnqueueAsync(IssueWriteOperation operation, CancellationToken ct = default);

    /// <summary>
    /// Gets the number of pending write operations in the queue.
    /// </summary>
    int PendingCount { get; }

    /// <summary>
    /// Gets a value indicating whether the queue is currently processing an operation.
    /// </summary>
    bool IsProcessing { get; }
}

/// <summary>
/// The type of write operation to persist.
/// </summary>
public enum WriteOperationType
{
    Create,
    Update,
    Delete
}

/// <summary>
/// Represents a queued write operation for issue persistence.
/// </summary>
/// <param name="IssueId">The ID of the issue being written.</param>
/// <param name="Type">The type of write operation.</param>
/// <param name="WriteAction">The async action that performs the actual persistence.</param>
/// <param name="QueuedAt">When the operation was queued.</param>
public record IssueWriteOperation(
    string IssueId,
    WriteOperationType Type,
    Func<CancellationToken, Task> WriteAction,
    DateTimeOffset QueuedAt);
