/**
 * MyAIAgent | Gateway · Presentation
 *
 * Defines the configuration values for public chat IP, visitor, and concurrent-stream limits.
 */

namespace MyAIAgent.Gateway.Presentation.PublicApi;

public sealed record PublicChatRateLimits(
    int IpPermitLimit = 60,
    int IpWindowMinutes = 10,
    int VisitorBurst = 20,
    int VisitorReplenishSeconds = 30,
    int MaxConcurrentStreams = 10,
    int MaxConcurrentStreamsPerVisitor = 2);
