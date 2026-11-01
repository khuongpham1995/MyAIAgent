/**
 * MyAIAgent | Console Client
 *
 * Bounds HTTP response reads before parsing JSON or SSE so oversized data cannot grow indefinitely.
 */

namespace MyAIAgent.ConsoleClient;

internal sealed class LimitedReadStream(Stream source, long limit) : Stream
{
    private long remaining = limit;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = await source.ReadAsync(buffer[..(int)Math.Min(buffer.Length, remaining + 1)], cancellationToken);
        remaining -= count;
        if (remaining < 0)
            throw new GatewayClientException("Gateway response exceeded the client size limit.");
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = source.Read(buffer, offset, (int)Math.Min(count, remaining + 1));
        remaining -= read;
        return remaining < 0 ? throw new GatewayClientException("Gateway response exceeded the client size limit.") : read;
    }

    // The response owns the underlying stream and disposes it on every completion/failure path.
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
