/**
 * MyAIAgent | Orchestrator · Application
 *
 * Signals that an explicitly requested agent is missing so chat cannot silently use an unconstrained fallback.
 */

namespace MyAIAgent.Orchestrator.Application.Common.Exceptions;

public sealed class AgentNotFoundException(string agentId)
    : Exception($"Agent '{agentId}' was not found.");
