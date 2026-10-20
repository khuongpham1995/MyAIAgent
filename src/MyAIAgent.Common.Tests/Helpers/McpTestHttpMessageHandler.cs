/**
 * MyAIAgent | Common · Tests
 *
 * Simulates MCP protocol responses and failures so discovery and transport tests do not need a live server.
 */

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MyAIAgent.Common.Tests;

public sealed class McpTestHttpMessageHandler(bool waitForCancellation = false, HttpStatusCode? failureStatus = null, bool failToolListing = false) : HttpMessageHandler
{
    public List<string> Methods { get; } = [];
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Started.TrySetResult();
        if (failureStatus is { } status)
            return new HttpResponseMessage(status);
        if (waitForCancellation)
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        if (request.Method != HttpMethod.Post)
            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);

        using var message = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var method = message.RootElement.GetProperty("method").GetString()!;
        Methods.Add(method);
        if (!message.RootElement.TryGetProperty("id", out var id))
            return new HttpResponseMessage(HttpStatusCode.Accepted);

        // Older servers negotiate through initialize instead of the newer stateless discovery method.
        if (method == "server/discover" || (method == "tools/list" && failToolListing))
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { jsonrpc = "2.0", id = id.Clone(), error = new { code = -32601, message = "Method not found" } })
            };

        object result = method switch
        {
            "initialize" => new
            {
                protocolVersion = message.RootElement.GetProperty("params").GetProperty("protocolVersion").GetString(),
                capabilities = new { tools = new { } },
                serverInfo = new { name = "test-server", version = "1.0" }
            },
            "tools/list" => new
            {
                tools = new[] { new { name = "test_tool", description = "Test tool", inputSchema = new { type = "object", properties = new { } } } }
            },
            "tools/call" => new { content = new[] { new { type = "text", text = "tool reply" } }, isError = false },
            _ => throw new InvalidOperationException($"Unexpected MCP method: {method}")
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = id.Clone(), result })
        };
    }
}
