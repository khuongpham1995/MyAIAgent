/**
 * MyAIAgent | Orchestrator · Application
 *
 * Defines ordinary and streaming model-reply operations consumed by chat handlers.
 */

using MyAIAgent.Common.Application.Models;
using MyAIAgent.Orchestrator.Application.Chat;

namespace MyAIAgent.Orchestrator.Application.Common.Interfaces;

public interface IChatOrchestratorService
{
    Task<ResponseModel<string>> ChatAsync(ChatRequest request, CancellationToken cancellationToken);

    IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, CancellationToken cancellationToken);
}
