/**
 * MyAIAgent | Common · Infrastructure
 *
 * Selects Azure CLI or explicit managed identity credentials and validates service-root HTTPS endpoints.
 */

using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace MyAIAgent.Common.Infrastructure.Extensions;

public static class AzureCredentialFactory
{
    public static TokenCredential Create(IConfiguration configuration, bool isDevelopment)
    {
        if (isDevelopment)
            return new AzureCliCredential();

        var clientId = configuration["AzureIdentity:ClientId"];
        if (!Guid.TryParse(clientId, out _))
            throw new InvalidOperationException("AzureIdentity:ClientId must identify the assigned managed identity outside Development.");

        // Deployed applications must never fall back to developer or environment credentials.
        return new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId));
    }

    public static Uri RequireHttpsEndpoint(string? value, string configurationKey)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment)
            || endpoint.AbsolutePath != "/")
            throw new InvalidOperationException($"{configurationKey} must be an HTTPS service endpoint without credentials, a path, query, or fragment.");

        return endpoint;
    }
}
