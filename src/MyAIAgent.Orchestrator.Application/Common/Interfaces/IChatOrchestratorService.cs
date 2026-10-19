using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Application.Chat;

namespace MyAIAgent.Orchestrator.Application.Common.Interfaces;

public interface IChatOrchestratorService
{
    Task<ResponseModel<string>> ChatAsync(ChatRequest request, CancellationToken cancellationToken);

    IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, CancellationToken cancellationToken);
}
