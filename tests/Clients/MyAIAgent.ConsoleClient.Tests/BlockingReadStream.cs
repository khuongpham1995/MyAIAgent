/**
 * MyAIAgent | Console Client · Tests
 *
 * Stalls after response headers to prove deadlines and user cancellation cover body reads.
 */

namespace MyAIAgent.ConsoleClient.Tests;

internal sealed class BlockingReadStream : MemoryStream
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool WasDisposed { get; private set; }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return 0;
    }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
}
