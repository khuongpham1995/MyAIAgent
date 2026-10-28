/**
 * MyAIAgent | Orchestrator · Application
 *
 * Carries owner-scoped conversation retrieval input through the Orchestrator MediatR application boundary.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.Orchestrator.Application.Conversations.Queries.GetConversation;

public sealed record GetConversationQuery(string SessionId, string UserId)
    : IRequest<ResponseModel<ConversationDetail>>;
