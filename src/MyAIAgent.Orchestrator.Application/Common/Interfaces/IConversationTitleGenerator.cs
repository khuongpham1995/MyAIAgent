/**
 * MyAIAgent | Orchestrator · Application
 *
 * Defines generation of a short title from the first completed chat turn.
 */

namespace MyAIAgent.Orchestrator.Application.Common.Interfaces;

public interface IConversationTitleGenerator
{
    Task<string> GenerateTitleAsync(string userMessage, string reply, CancellationToken cancellationToken);
}
