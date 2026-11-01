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

    private McpServiceClientSettings(Uri origin, string? accessKey, bool isDevelopment, IReadOnlyList<Uri> externalOrigins)
    {
        Origin = origin;
        this.accessKey = accessKey;
        IsDevelopment = isDevelopment;
        AllowedExternalOrigins = externalOrigins;
    }

    public Uri Origin { get; }
    public bool IsDevelopment { get; }
    public IReadOnlyList<Uri> AllowedExternalOrigins { get; }

    public static McpServiceClientSettings Load(IConfiguration configuration, bool isDevelopment)
    {
        var value = configuration["McpServer:BaseUrl"];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var origin)
            || (origin.Scheme != Uri.UriSchemeHttps
                && !(isDevelopment && origin.Scheme == Uri.UriSchemeHttp && origin.IsLoopback))
            || !OutboundAddressPolicy.HasSafeAuthority(origin) || origin.IdnHost.Contains('*') || origin.AbsolutePath != "/"
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

        var section = configuration.GetSection("McpServer:AllowedExternalOrigins");
        // The JSON configuration provider represents an empty array as an empty section value.
        if (!string.IsNullOrEmpty(section.Value))
            throw new InvalidOperationException("McpServer:AllowedExternalOrigins must be an array of HTTPS origins.");
        var externalOrigins = new List<Uri>();
        foreach (var entry in section.GetChildren())
        {
            if (!Uri.TryCreate(entry.Value, UriKind.Absolute, out var external)
                || external.Scheme != Uri.UriSchemeHttps || !OutboundAddressPolicy.IsPublicUri(external)
                || external.AbsolutePath != "/" || external.Query.Length != 0
                || external.IdnHost.Contains('*') || SameOrigin(origin, external))
                throw new InvalidOperationException("McpServer:AllowedExternalOrigins must contain public HTTPS origins without credentials, paths, queries, fragments, or wildcards.");
            externalOrigins.Add(external);
        }
        return new McpServiceClientSettings(origin, key, isDevelopment, externalOrigins.AsReadOnly());
    }

    public bool IsTrustedEndpoint(Uri endpoint) => IsTrustedEndpoint(Origin, endpoint);

    public bool IsAllowedExternalEndpoint(Uri endpoint) => endpoint.IsAbsoluteUri
        && endpoint.Scheme == Uri.UriSchemeHttps && OutboundAddressPolicy.IsPublicUri(endpoint)
        && AllowedExternalOrigins.Any(origin => SameOrigin(origin, endpoint));

    public bool IsAllowedEndpoint(string? value) => string.IsNullOrEmpty(value)
        || (Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
            && (IsTrustedEndpoint(endpoint) || IsAllowedExternalEndpoint(endpoint)));

    public bool IsAllowedServiceAddress(System.Net.IPAddress address) =>
        (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6 || address.ScopeId == 0)
        && (OutboundAddressPolicy.IsPublicAddress(address)
            || OutboundAddressPolicy.IsPrivateAddress(address)
            || (IsDevelopment && Origin.IsLoopback && System.Net.IPAddress.IsLoopback(OutboundAddressPolicy.Normalize(address))));

    private static bool SameOrigin(Uri origin, Uri endpoint) => endpoint.Scheme == origin.Scheme
        && string.Equals(endpoint.IdnHost, origin.IdnHost, StringComparison.OrdinalIgnoreCase)
        && endpoint.Port == origin.Port;

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
            throw new OutboundDestinationBlockedException("trusted-endpoint");
        if (request.Headers.Host is { } host
            && !string.Equals(host, endpoint.Authority, StringComparison.OrdinalIgnoreCase))
            throw new OutboundDestinationBlockedException("host-override");
        if (!string.IsNullOrEmpty(accessKey))
            request.Headers.Add("Authorization", accessKey);
    }
}
