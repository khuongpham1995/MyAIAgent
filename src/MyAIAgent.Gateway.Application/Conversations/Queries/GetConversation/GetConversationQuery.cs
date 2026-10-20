/**
 * MyAIAgent | Gateway · Application
 *
 * Carries owner-scoped conversation retrieval input through the Gateway MediatR application boundary.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Gateway.Application.Conversations.Queries.GetConversation;

public sealed record GetConversationQuery(string SessionId, string UserId)
    : IRequest<ResponseModel<ConversationDetail>>;
