/**
 * MyAIAgent | Console Client
 *
 * Applies local testing error and size policies without coupling the reusable generated SDK to this UI.
 */

namespace MyAIAgent.ConsoleClient;

internal sealed class GatewayResponseHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var accepted = false;
        try
        {
            GatewayClient.EnsureStatus(response);
            // The caller chooses streaming; an untrusted response label cannot relax JSON limits.
            if (!request.Headers.Accept.Any(header => string.Equals(header.MediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase)))
                response.Content = new LimitedHttpContent(response.Content);
            accepted = true;
            return response;
        }
        finally
        {
            if (!accepted) response.Dispose();
        }
    }
}
