/**
 * MyAIAgent | Common · Infrastructure
 *
 * Validates the trusted MCP origin and scopes its credential to the service's protocol endpoints.
 */

using Microsoft.Extensions.Configuration;

namespace MyAIAgent.Common.Infrastructure.Http;

public sealed class McpServiceClientSettings
{
    private readonly string? accessKey;

    private McpServiceClientSettings(Uri origin, string? accessKey)
    {
        Origin = origin;
        this.accessKey = accessKey;
    }

    public Uri Origin { get; }

    public static McpServiceClientSettings Load(IConfiguration configuration, bool isDevelopment)
    {
        var value = configuration["McpServer:BaseUrl"];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var origin)
            || (origin.Scheme != Uri.UriSchemeHttps
                && !(isDevelopment && origin.Scheme == Uri.UriSchemeHttp && origin.IsLoopback))
            || origin.UserInfo.Length != 0 || origin.AbsolutePath != "/"
            || origin.Query.Length != 0 || origin.Fragment.Length != 0)
        {
            throw new InvalidOperationException(
                "McpServer:BaseUrl must be an HTTPS origin without credentials, a path, query, or fragment; Development also permits loopback HTTP.");
        }

        var key = configuration["McpServer:AccessKey"];
        if (!isDevelopment && string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("McpServer:AccessKey is required outside Development.");
        if (!string.IsNullOrEmpty(key) && key.Any(character => character <= ' ' || character > '~'))
            throw new InvalidOperationException("McpServer:AccessKey must contain only visible ASCII characters.");

        return new McpServiceClientSettings(origin, key);
    }

    public bool IsTrustedEndpoint(Uri endpoint) => IsTrustedEndpoint(Origin, endpoint);

    public static bool IsTrustedEndpoint(Uri origin, Uri endpoint) =>
        endpoint.IsAbsoluteUri
        && endpoint.Scheme == origin.Scheme
        && string.Equals(endpoint.IdnHost, origin.IdnHost, StringComparison.OrdinalIgnoreCase)
        && endpoint.Port == origin.Port
        && endpoint.UserInfo.Length == 0
        && endpoint.Query.Length == 0
        && endpoint.Fragment.Length == 0
        && endpoint.AbsolutePath is "/" or "/mcp" or "/mcp/";

    internal void Authenticate(HttpRequestMessage request)
    {
        // The shared credential never belongs on an arbitrary agent endpoint or an overridden virtual host.
        request.Headers.Remove("Authorization");
        if (request.RequestUri is not { } endpoint || !IsTrustedEndpoint(endpoint))
            return;
        if (request.Headers.Host is { } host
            && !string.Equals(host, endpoint.Authority, StringComparison.OrdinalIgnoreCase))
            throw new HttpRequestException("MCP requests cannot override the trusted service host.");
        if (!string.IsNullOrEmpty(accessKey))
            request.Headers.Add("Authorization", accessKey);
    }
}
