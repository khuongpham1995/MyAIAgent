using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Domain.Models;

namespace MyAIAgent.Orchestrator.Domain.Interfaces;

public interface IChatOrchestratorService
{
    Task<ResponseModel<string>> ChatAsync(ChatRequest request, CancellationToken cancellationToken);
}
