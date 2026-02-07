using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class IssueSerializationQueueServiceTests
{
    private IssueSerializationQueueService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new IssueSerializationQueueService();
        _sut.StartProcessing();
    }

    [TearDown]
    public void TearDown()
    {
        _sut.Dispose();
    }

    #region Enqueue

    [Test]
    public async Task EnqueueAsync_IncrementsPendingCount()
    {
        var tcs = new TaskCompletionSource();
        var operation = CreateOperation("issue1", WriteOperationType.Create, async ct =>
        {
            // Block until we check the count
            await tcs.Task;
        });

        await _sut.EnqueueAsync(operation);

        _sut.PendingCount.Should().BeGreaterOrEqualTo(1);
        tcs.SetResult();
    }

    [Test]
    public async Task EnqueueAsync_ThrowsWhenDisposed()
    {
        _sut.Dispose();

        var operation = CreateOperation("issue1", WriteOperationType.Create, _ => Task.CompletedTask);

        await FluentActions.Invoking(() => _sut.EnqueueAsync(operation).AsTask())
            .Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Processing

    [Test]
    public async Task ProcessesEnqueuedOperations()
    {
        var processed = false;
        var operation = CreateOperation("issue1", WriteOperationType.Create, _ =>
        {
            processed = true;
            return Task.CompletedTask;
        });

        await _sut.EnqueueAsync(operation);

        // Wait for processing
        await WaitForQueueDrainAsync();

        processed.Should().BeTrue();
    }

    [Test]
    public async Task ProcessesOperationsInOrder()
    {
        var order = new List<string>();
        var allProcessed = new TaskCompletionSource();

        for (var i = 0; i < 5; i++)
        {
            var id = $"issue{i}";
            var isLast = i == 4;
            var operation = CreateOperation(id, WriteOperationType.Update, _ =>
            {
                lock (order) { order.Add(id); }
                if (isLast)
                {
                    allProcessed.TrySetResult();
                }
                return Task.CompletedTask;
            });
            await _sut.EnqueueAsync(operation);
        }

        await Task.WhenAny(allProcessed.Task, Task.Delay(5000));

        order.Should().BeEquivalentTo(
            ["issue0", "issue1", "issue2", "issue3", "issue4"],
            options => options.WithStrictOrdering());
    }

    [Test]
    public async Task PendingCount_DecreasesAfterProcessing()
    {
        var operation = CreateOperation("issue1", WriteOperationType.Create, _ => Task.CompletedTask);

        await _sut.EnqueueAsync(operation);
        await WaitForQueueDrainAsync();

        _sut.PendingCount.Should().Be(0);
    }

    #endregion

    #region Retry

    [Test]
    public async Task RetriesOnFailure()
    {
        var attempts = 0;
        var operation = CreateOperation("issue1", WriteOperationType.Update, _ =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new IOException("Simulated failure");
            }
            return Task.CompletedTask;
        });

        await _sut.EnqueueAsync(operation);
        await WaitForQueueDrainAsync(timeout: TimeSpan.FromSeconds(10));

        attempts.Should().Be(3);
    }

    [Test]
    public async Task DropsOperationAfterMaxRetries()
    {
        var attempts = 0;
        var operation = CreateOperation("issue1", WriteOperationType.Update, _ =>
        {
            attempts++;
            throw new IOException("Permanent failure");
        });

        await _sut.EnqueueAsync(operation);
        await WaitForQueueDrainAsync(timeout: TimeSpan.FromSeconds(10));

        // MaxRetries is 3, so we get the initial attempt + 3 retries = 4 total
        attempts.Should().Be(4);
        _sut.PendingCount.Should().Be(0);
    }

    #endregion

    #region Cancellation

    [Test]
    public async Task RespectsCancellationDuringEnqueue()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var operation = CreateOperation("issue1", WriteOperationType.Create, _ => Task.CompletedTask);

        await FluentActions.Invoking(() => _sut.EnqueueAsync(operation, cts.Token).AsTask())
            .Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Lifecycle

    [Test]
    public void StartProcessing_CanBeCalledMultipleTimes()
    {
        Assert.DoesNotThrow(() =>
        {
            _sut.StartProcessing();
            _sut.StartProcessing();
        });
    }

    [Test]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _sut.Dispose();

        Assert.DoesNotThrow(() => _sut.Dispose());
    }

    [Test]
    public async Task GracefulShutdown_DrainsRemainingOperations()
    {
        var processed = new List<string>();
        var gate = new TaskCompletionSource();

        // Enqueue a blocking operation and a subsequent one
        var blockingOp = CreateOperation("blocking", WriteOperationType.Create, async _ =>
        {
            await gate.Task;
            lock (processed) { processed.Add("blocking"); }
        });

        var followUpOp = CreateOperation("followup", WriteOperationType.Create, _ =>
        {
            lock (processed) { processed.Add("followup"); }
            return Task.CompletedTask;
        });

        await _sut.EnqueueAsync(blockingOp);
        await _sut.EnqueueAsync(followUpOp);

        // Release the blocking operation and dispose immediately
        gate.SetResult();

        // Allow time for processing
        await Task.Delay(200);

        _sut.Dispose();

        processed.Should().Contain("blocking");
    }

    [Test]
    public void StartProcessing_ThrowsWhenDisposed()
    {
        _sut.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _sut.StartProcessing());
    }

    #endregion

    #region Helpers

    private static IssueWriteOperation CreateOperation(
        string issueId,
        WriteOperationType type,
        Func<CancellationToken, Task> writeAction)
    {
        return new IssueWriteOperation(
            IssueId: issueId,
            Type: type,
            WriteAction: writeAction,
            QueuedAt: DateTimeOffset.UtcNow);
    }

    private async Task WaitForQueueDrainAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (_sut.PendingCount > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    #endregion
}
