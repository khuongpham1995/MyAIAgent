/**
 * MyAIAgent | Console Client
 *
 * Preserves the JSON response size limit when the generated client reads or buffers HTTP content.
 */

using System.Net;

namespace MyAIAgent.ConsoleClient;

internal sealed class LimitedHttpContent : HttpContent
{
    private readonly HttpContent original;

    public LimitedHttpContent(HttpContent original)
    {
        if (original.Headers.ContentLength > 2 * 1024 * 1024)
            throw new GatewayClientException("Gateway response exceeded the client size limit.");
        this.original = original;
        foreach (var header in original.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
    }

    protected override async Task<Stream> CreateContentReadStreamAsync() =>
        new LimitedReadStream(await original.ReadAsStreamAsync(), 2 * 1024 * 1024);

    protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
        new LimitedReadStream(await original.ReadAsStreamAsync(cancellationToken), 2 * 1024 * 1024);

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        await using var limited = new LimitedReadStream(await original.ReadAsStreamAsync(cancellationToken), 2 * 1024 * 1024);
        await limited.CopyToAsync(stream, cancellationToken);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) original.Dispose();
        base.Dispose(disposing);
    }
}
