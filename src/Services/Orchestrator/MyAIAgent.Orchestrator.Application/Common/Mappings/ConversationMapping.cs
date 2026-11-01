/**
 * MyAIAgent | Orchestrator · Application
 *
 * Maps conversation response projections and rename inputs at the owning application boundary.
 */

using Mapster;
using MyAIAgent.Orchestrator.Application.Conversations;
using MyAIAgent.Orchestrator.Application.Conversations.Commands.RenameConversation;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Application.Common.Mappings;

public sealed class ConversationMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<RenameConversationRequest, RenameConversationCommand>()
            .MapWith(source => new RenameConversationCommand(
                source.SessionId,
                source.Body.UserId,
                source.Body.Title));

        config.NewConfig<ChatSession, ConversationDetail>()
            .MapWith(source => new ConversationDetail(
                source.SessionId,
                source.Title,
                source.UpdatedAt,
                source.Messages
                    .OrderBy(message => message.CreatedAt)
                    .Select(message => new ConversationMessage(message.Role, message.Content, message.CreatedAt))
                    .ToList()));
    }
}
