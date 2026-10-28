/**
 * MyAIAgent | Gateway · Presentation
 *
 * Validates optional public-chat JWT and origin settings, rejecting partial configuration at startup.
 */

using System.Text;
using MyAIAgent.Gateway.Domain.Constants;

namespace MyAIAgent.Gateway.Presentation.PublicApi;

public sealed record PublicChatSettings(
    string Issuer,
    string Audience,
    string? JwksUri,
    string? SigningKey,
    IReadOnlyList<string> AllowedOrigins,
    string? AgentId,
    bool TrustForwardedHeaders,
    PublicChatRateLimits RateLimits)
{
    public const int MinSigningKeyBytes = 32;

    /// <summary>
    /// Returns null when no JWT setting is present (public chat disabled). Any partial or
    /// ambiguous configuration throws so a half-configured gateway never starts.
    /// </summary>
    public static PublicChatSettings? FromConfiguration(IConfiguration configuration, bool isDevelopment)
    {
        var issuer = configuration[GatewayConstant.PublicChat.JwtIssuer];
        var audience = configuration[GatewayConstant.PublicChat.JwtAudience];
        var jwksUri = configuration[GatewayConstant.PublicChat.JwtJwksUri];
        var signingKey = configuration[GatewayConstant.PublicChat.JwtSigningKey];

        if (new[] { issuer, audience, jwksUri, signingKey }.All(string.IsNullOrWhiteSpace))
            return null;

        if (string.IsNullOrWhiteSpace(issuer))
            throw new InvalidOperationException($"{GatewayConstant.PublicChat.JwtIssuer} is required for public chat.");
        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException($"{GatewayConstant.PublicChat.JwtAudience} is required for public chat.");

        var hasJwks = !string.IsNullOrWhiteSpace(jwksUri);
        var hasKey = !string.IsNullOrWhiteSpace(signingKey);
        if (hasJwks == hasKey)
            throw new InvalidOperationException(
                $"Set exactly one of {GatewayConstant.PublicChat.JwtJwksUri} or {GatewayConstant.PublicChat.JwtSigningKey}.");

        if (hasKey && Encoding.UTF8.GetByteCount(signingKey!) < MinSigningKeyBytes)
            throw new InvalidOperationException(
                $"{GatewayConstant.PublicChat.JwtSigningKey} must be at least {MinSigningKeyBytes} bytes.");

        if (hasJwks && !IsAllowedUri(jwksUri!, isDevelopment))
            throw new InvalidOperationException(
                $"{GatewayConstant.PublicChat.JwtJwksUri} must be an absolute HTTPS URL.");

        var agentId = configuration[GatewayConstant.PublicChat.AgentId];
        if (!isDevelopment && string.IsNullOrWhiteSpace(agentId))
            throw new InvalidOperationException($"{GatewayConstant.PublicChat.AgentId} is required in production.");

        return new PublicChatSettings(
            issuer,
            audience,
            hasJwks ? jwksUri : null,
            hasKey ? signingKey : null,
            ReadOrigins(configuration, isDevelopment),
            string.IsNullOrWhiteSpace(agentId) ? null : agentId,
            configuration.GetValue<bool>("PublicChat:TrustForwardedHeaders"),
            configuration.GetSection("PublicChat:RateLimits").Get<PublicChatRateLimits>() ?? new PublicChatRateLimits());
    }

    private static List<string> ReadOrigins(IConfiguration configuration, bool isDevelopment)
    {
        var configured = configuration.GetSection(GatewayConstant.PublicChat.AllowedOrigins).Get<string[]>();
        var origins = (configured ?? [])
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .ToList();

        // No built-in origin: the caller must name every site allowed to use the API from a browser.
        if (origins.Count == 0)
            throw new InvalidOperationException($"{GatewayConstant.PublicChat.AllowedOrigins} is required for public chat.");

        foreach (var origin in origins)
        {
            var isValid = !origin.Contains('*')
                && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && uri.AbsolutePath == "/"
                && (uri.Scheme == Uri.UriSchemeHttps || (isDevelopment && uri.Scheme == Uri.UriSchemeHttp));

            if (!isValid)
                throw new InvalidOperationException(
                    $"{GatewayConstant.PublicChat.AllowedOrigins} contains an invalid origin: '{origin}'.");
        }

        return origins;
    }

    private static bool IsAllowedUri(string value, bool isDevelopment) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (isDevelopment && uri.Scheme == Uri.UriSchemeHttp));
}
