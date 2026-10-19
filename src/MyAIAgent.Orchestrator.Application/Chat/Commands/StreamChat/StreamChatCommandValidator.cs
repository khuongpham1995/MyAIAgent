using FluentValidation;

namespace MyAIAgent.Orchestrator.Application.Chat.Commands.StreamChat;

public sealed class StreamChatCommandValidator : AbstractValidator<StreamChatCommand>
{
    public StreamChatCommandValidator()
    {
        RuleFor(x => x.UserMessage)
            .NotEmpty().WithMessage("User message is required.")
            .MaximumLength(4000).WithMessage("User message must not exceed 4000 characters.");
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");
    }
}
