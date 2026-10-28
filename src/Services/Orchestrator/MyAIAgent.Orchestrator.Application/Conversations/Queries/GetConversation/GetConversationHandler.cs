/**
 * MyAIAgent | Orchestrator · Application
 *
 * Checks ownership and returns conversation metadata with ordered messages.
 */

using MyAIAgent.Common.Application.Models;
using MapsterMapper;
using MediatR;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Conversations.Queries.GetConversation;

public sealed class GetConversationHandler(IChatSessionRepository sessionRepository, IMapper mapper)
    : IRequestHandler<GetConversationQuery, ResponseModel<ConversationDetail>>
{
    public async Task<ResponseModel<ConversationDetail>> Handle(
        GetConversationQuery query,
        CancellationToken cancellationToken)
    {
        var session = await sessionRepository.FindForUserAsync(query.SessionId, query.UserId, cancellationToken);

        // The lookup is scoped to the owner; the check below is kept as defense in depth.
        if (session is null || session.UserId != query.UserId)
            return ResponseModel<ConversationDetail>.Fail(message: "Conversation not found.");

        return ResponseModel<ConversationDetail>.Succeed(mapper.Map<ConversationDetail>(session));
    }
}
