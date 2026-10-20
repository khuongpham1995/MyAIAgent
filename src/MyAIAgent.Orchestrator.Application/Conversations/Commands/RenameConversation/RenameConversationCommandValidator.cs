/**
 * MyAIAgent | Orchestrator · Application
 *
 * Validates SessionId, UserId, Title before owner-scoped conversation renaming reaches the Orchestrator handler.
 */

using FluentValidation;

namespace MyAIAgent.Orchestrator.Application.Conversations.Commands.RenameConversation;

public sealed class RenameConversationCommandValidator : AbstractValidator<RenameConversationCommand>
{
    public RenameConversationCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session id is required.");
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(100).WithMessage("Title must not exceed 100 characters.");
    }
}
