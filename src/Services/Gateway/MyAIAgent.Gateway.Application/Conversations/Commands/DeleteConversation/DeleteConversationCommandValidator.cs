/**
 * MyAIAgent | Gateway · Application
 *
 * Validates SessionId, UserId before owner-scoped conversation deletion reaches the Gateway handler.
 */

using FluentValidation;
using MyAIAgent.Gateway.Domain.Constants;

namespace MyAIAgent.Gateway.Application.Conversations.Commands.DeleteConversation;

public sealed class DeleteConversationCommandValidator : AbstractValidator<DeleteConversationCommand>
{
    public DeleteConversationCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session id is required.")
            .Matches(GatewayConstant.SessionIdPattern).WithMessage("Session id is invalid.");
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");
    }
}
