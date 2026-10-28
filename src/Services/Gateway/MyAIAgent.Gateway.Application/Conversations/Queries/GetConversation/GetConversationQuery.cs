/**
 * MyAIAgent | Gateway · Application
 *
 * Carries owner-scoped conversation retrieval input through the Gateway MediatR application boundary.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.Gateway.Application.Conversations.Queries.GetConversation;

public sealed record GetConversationQuery(string SessionId, string UserId)
    : IRequest<ResponseModel<ConversationDetail>>;
