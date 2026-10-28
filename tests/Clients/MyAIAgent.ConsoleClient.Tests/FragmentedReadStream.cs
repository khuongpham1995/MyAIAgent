/**
 * MyAIAgent | Console Client · Tests
 *
 * Delivers HTTP data one byte at a time to exercise Unicode and SSE framing across network reads.
 */

namespace MyAIAgent.ConsoleClient.Tests;

internal sealed class FragmentedReadStream(byte[] bytes) : MemoryStream(bytes)
{
    public bool WasDisposed { get; private set; }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        base.ReadAsync(buffer, offset, Math.Min(count, 1), cancellationToken);

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
}
