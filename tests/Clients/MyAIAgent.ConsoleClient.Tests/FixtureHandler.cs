/**
 * MyAIAgent | Console Client · Tests
 *
 * Substitutes the network boundary while preserving real request serialization and cancellation.
 */

namespace MyAIAgent.ConsoleClient.Tests;

internal sealed class FixtureHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond,
    List<CapturedRequest> requests) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        requests.Add(new CapturedRequest(request.Method.Method, request.RequestUri!.PathAndQuery,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
            request.Headers.Authorization is not null, request.Headers.Accept.ToString()));
        return await respond(request, cancellationToken);
    }
}
