/**
 * MyAIAgent | Console Client · Tests
 *
 * Builds the real typed client with a controllable HTTP boundary and records requests.
 */

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace MyAIAgent.ConsoleClient.Tests;

internal sealed class GatewayFixture : IDisposable
{
    private readonly ServiceProvider provider;
    public List<CapturedRequest> Requests { get; } = [];
    public GatewayClient Client { get; }
    public ClientOptions Options { get; }

    public GatewayFixture(Func<HttpRequestMessage, HttpResponseMessage> respond, ClientOptions? options = null)
        : this((request, _) => Task.FromResult(respond(request)), options) { }

    public GatewayFixture(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond, ClientOptions? options = null)
    {
        var services = new ServiceCollection();
        Options = options ?? new ClientOptions(new Uri("https://localhost:7200"))
        {
            McpServerEndpoint = new Uri("http://localhost:5255/mcp"),
        };
        services.AddConsoleClient(Options)
            .ConfigurePrimaryHttpMessageHandler(() => new FixtureHandler(respond, Requests));
        provider = services.BuildServiceProvider();
        Client = provider.GetRequiredService<GatewayClient>();
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Stream(string body) => Stream(new FragmentedReadStream(Encoding.UTF8.GetBytes(body)));

    public static HttpResponseMessage Stream(Stream body)
    {
        var content = new StreamContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    public void Dispose() => provider.Dispose();
}
