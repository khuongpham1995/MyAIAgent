/**
 * MyAIAgent | Orchestrator · Application
 *
 * Carries paginated conversation listing input through the Orchestrator MediatR application boundary.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.Orchestrator.Application.Conversations.Queries.ListConversations;

public sealed record ListConversationsQuery(string UserId, int Page = 1, int PageSize = 20)
    : IRequest<ResponseModel<IReadOnlyList<ConversationSummary>>>;
