namespace MyAIAgent.Orchestrator.Application.Common.Interfaces;

public interface IConversationTitleGenerator
{
    Task<string> GenerateTitleAsync(string userMessage, string reply, CancellationToken cancellationToken);
}
