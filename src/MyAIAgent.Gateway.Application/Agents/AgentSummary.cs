/**
 * MyAIAgent | Gateway · Application
 *
 * Projects agent identity, instructions, endpoint, and enabled tools into an application response.
 */

namespace MyAIAgent.Gateway.Application.Agents;

public sealed record AgentSummary(
    string Id,
    string Name,
    string Instructions,
    string McpServerEndpoint,
    bool IsDefault,
    IReadOnlyList<string> EnabledTools);
