/**
 * MyAIAgent | Common · Infrastructure
 *
 * Checks every protocol request and normalizes connection-policy failures before callers can silently fall back.
 */

using System.Net;

namespace MyAIAgent.Common.Infrastructure.Http;

public sealed class OutboundRequestHandler(Func<Uri, bool> isAllowed, bool anonymous = false, bool rejectRedirects = true)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (anonymous)
        {
            request.Headers.Remove("Authorization");
            request.Headers.Remove("Proxy-Authorization");
        }
        if (request.RequestUri is not { } uri || !isAllowed(uri))
            throw new OutboundDestinationBlockedException("uri");
        if (request.Headers.Host is { } host && !string.Equals(host, uri.Authority, StringComparison.OrdinalIgnoreCase))
            throw new OutboundDestinationBlockedException("host-override");
        // HTTP/3 does not use SocketsHttpHandler.ConnectCallback. MCP and page fetching work over HTTP/1.1.
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (rejectRedirects && (int)response.StatusCode is >= 300 and <= 399)
            {
                response.Dispose();
                throw new OutboundDestinationBlockedException("redirect");
            }
            return response;
        }
        catch (HttpRequestException ex) when (OutboundDestinationBlockedException.Find(ex) is not null)
        {
            throw new OutboundDestinationBlockedException(OutboundDestinationBlockedException.Find(ex)!.Reason);
        }
    }
}
