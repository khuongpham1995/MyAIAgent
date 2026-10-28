/**
 * MyAIAgent | Gateway · Application
 *
 * Returns the generated reply and session identifier from Gateway chat forwarding.
 */

namespace MyAIAgent.Gateway.Application.Chat;

public sealed record ChatGatewayResponse(string Reply, string SessionId);
