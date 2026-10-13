using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Interfaces;

public interface IChatOrchestratorService
{
    Task<ResponseModel<string>> ChatAsync(ChatRequest request, CancellationToken cancellationToken);
}
