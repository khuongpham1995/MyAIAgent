namespace MyAIAgent.Gateway.Application.Chat;

// AgentId comes from server configuration so a public caller can never pick the agent.
public sealed record PublicChatGatewayRequest(PublicChatRequestDto Request, string UserId, string? AgentId);
