/**
 * MyAIAgent | Orchestrator · Application
 *
 * Validates SessionId, UserId before owner-scoped conversation deletion reaches the Orchestrator handler.
 */

using FluentValidation;

namespace MyAIAgent.Orchestrator.Application.Conversations.Commands.DeleteConversation;

public sealed class DeleteConversationCommandValidator : AbstractValidator<DeleteConversationCommand>
{
    public DeleteConversationCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session id is required.");
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");
    }
}
