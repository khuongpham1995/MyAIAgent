/**
 * MyAIAgent | Common · Infrastructure
 *
 * Adds an MCP service key only to trusted requests and sanitizes authentication failures.
 */

using System.Net;

namespace MyAIAgent.Common.Infrastructure.Http;

public sealed class McpServiceKeyHandler(McpServiceClientSettings settings) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        settings.Authenticate(request);
        var response = await base.SendAsync(request, cancellationToken);
        if (settings.IsTrustedEndpoint(request.RequestUri!)
            && response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            var status = response.StatusCode;
            response.Dispose();
            throw new HttpRequestException("MCP service authentication failed.", null, status);
        }

        return response;
    }
}
