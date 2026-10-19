namespace MyAIAgent.Orchestrator.Application.Common.Exceptions;

public sealed class AgentNotFoundException(string agentId)
    : Exception($"Agent '{agentId}' was not found.");
