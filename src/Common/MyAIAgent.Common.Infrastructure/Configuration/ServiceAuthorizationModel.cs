/**
 * MyAIAgent | Common · Infrastructure
 *
 * Holds the shared service-key authentication settings, including active and rotation keys.
 */

namespace MyAIAgent.Common.Infrastructure.Configuration;

public sealed record ServiceAuthorizationModel
{
    public bool Enabled { get; init; }
    public string AccessKey1 { get; init; } = string.Empty;
    public string AccessKey2 { get; init; } = string.Empty;
    public string HttpHeaderName { get; init; } = "Authorization";
}
