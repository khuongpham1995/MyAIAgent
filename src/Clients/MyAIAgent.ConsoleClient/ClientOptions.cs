/**
 * MyAIAgent | Console Client
 *
 * Validates the local Gateway and optional agent-creation MCP endpoint, with bounded request deadlines.
 */

using System.Net;
using Microsoft.Extensions.Configuration;

namespace MyAIAgent.ConsoleClient;

internal sealed record ClientOptions(Uri Gateway)
{
    public Uri? McpServerEndpoint { get; init; }
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan ChatTimeout { get; init; } = TimeSpan.FromMinutes(3);

    public static ClientOptions FromConfiguration(IConfiguration configuration)
    {
        var value = configuration["Gateway:BaseUrl"];
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Gateway:BaseUrl is required in configuration.");

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http")
            || !(uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || IPAddress.TryParse(uri.IdnHost.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip))
            || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/"
            || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Gateway must be an HTTP(S) loopback service root without credentials, query, or fragment.");

        Uri? mcpEndpoint = null;
        var endpoint = configuration["McpServer:Endpoint"];
        if (!string.IsNullOrWhiteSpace(endpoint)
            && (!Uri.TryCreate(endpoint, UriKind.Absolute, out mcpEndpoint)
                || mcpEndpoint.Scheme is not ("https" or "http")
                || mcpEndpoint.UserInfo.Length != 0 || mcpEndpoint.Fragment.Length != 0))
            throw new ArgumentException("McpServer:Endpoint must be an HTTP(S) URL without credentials or fragment.");

        return new ClientOptions(uri) { McpServerEndpoint = mcpEndpoint };
    }
}
