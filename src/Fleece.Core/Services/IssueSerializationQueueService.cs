using System.Threading.Channels;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Processes queued issue write operations asynchronously using a Channel-based processing loop.
/// Implements a write-through cache pattern where writes are queued and persisted
/// to disk in the background, unblocking the caller.
/// <para>
/// This service does not depend on Microsoft.Extensions.Hosting; it uses a manual Task-based
/// processing loop started via <see cref="StartProcessing"/> and stopped via <see cref="Dispose"/>.
/// </para>
/// </summary>
public sealed class IssueSerializationQueueService : IIssueSerializationQueue
{
    private readonly Channel<IssueWriteOperation> _channel;
    private readonly CancellationTokenSource _cts;
    private Task? _processingTask;
    private int _pendingCount;
    private volatile bool _isProcessing;
    private bool _disposed;

    private const int MaxRetries = 3;
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ShutdownDrainTimeout = TimeSpan.FromSeconds(10);

    public IssueSerializationQueueService()
    {
        _cts = new CancellationTokenSource();
        _channel = Channel.CreateUnbounded<IssueWriteOperation>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public int PendingCount => _pendingCount;

    /// <inheritdoc />
    public bool IsProcessing => _isProcessing;

    /// <summary>
    /// Starts the background processing loop. Must be called once before enqueuing operations.
    /// Safe to call multiple times; only the first call starts the loop.
    /// </summary>
    public void StartProcessing()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_processingTask is not null)
        {
            return;
        }

        _processingTask = Task.Run(() => ProcessLoopAsync(_cts.Token));
    }

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(IssueWriteOperation operation, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _channel.Writer.WriteAsync(operation, ct);
        Interlocked.Increment(ref _pendingCount);
    }

    private async Task ProcessLoopAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var operation in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessOperationAsync(operation, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await DrainQueueAsync();
        }
    }

    private async Task ProcessOperationAsync(IssueWriteOperation operation, CancellationToken ct)
    {
        _isProcessing = true;
        try
        {
            for (var attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await operation.WriteAction(ct);
                    return;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception) when (attempt < MaxRetries)
                {
                    var delay = BaseRetryDelay * Math.Pow(2, attempt);
                    await Task.Delay(delay, ct);
                }
                catch (Exception)
                {
                    // Max retries exceeded — drop the operation
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _pendingCount);
            _isProcessing = _pendingCount > 0;
        }
    }

    private async Task DrainQueueAsync()
    {
        using var drainCts = new CancellationTokenSource(ShutdownDrainTimeout);
        try
        {
            while (_channel.Reader.TryRead(out var operation))
            {
                await ProcessOperationAsync(operation, drainCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Drain timed out — remaining operations are lost
        }
    }

    /// <summary>
    /// Stops the processing loop and drains any remaining operations.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _channel.Writer.TryComplete();
        _cts.Cancel();

        // Wait for the processing loop to finish (with timeout)
        if (_processingTask is not null)
        {
            try
            {
                _processingTask.Wait(ShutdownDrainTimeout);
            }
            catch (AggregateException)
            {
                // Expected during shutdown
            }
        }

        _cts.Dispose();
    }
}
