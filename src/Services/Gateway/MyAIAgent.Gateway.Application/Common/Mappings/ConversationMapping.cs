/**
 * MyAIAgent | Gateway · Application
 *
 * Maps conversation response projections and rename inputs at the owning application boundary.
 */

using Mapster;
using MyAIAgent.Gateway.Application.Conversations;
using MyAIAgent.Gateway.Application.Conversations.Commands.RenameConversation;

namespace MyAIAgent.Gateway.Application.Common.Mappings;

public sealed class ConversationMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<RenameConversationRequest, RenameConversationCommand>()
            .MapWith(source => new RenameConversationCommand(
                source.SessionId,
                source.UserId,
                source.Body.Title));
    }
}
