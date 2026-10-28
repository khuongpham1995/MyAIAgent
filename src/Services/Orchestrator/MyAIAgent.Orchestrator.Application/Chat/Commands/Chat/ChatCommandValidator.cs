/**
 * MyAIAgent | Orchestrator · Application
 *
 * Validates UserMessage, UserId before a persisted chat turn reaches the Orchestrator handler.
 */

using FluentValidation;

namespace MyAIAgent.Orchestrator.Application.Chat.Commands.Chat;

public sealed class ChatCommandValidator : AbstractValidator<ChatCommand>
{
    public ChatCommandValidator()
    {
        RuleFor(x => x.UserMessage)
            .NotEmpty().WithMessage("User message is required.")
            .MaximumLength(4000).WithMessage("User message must not exceed 4000 characters.");
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");
    }
}
