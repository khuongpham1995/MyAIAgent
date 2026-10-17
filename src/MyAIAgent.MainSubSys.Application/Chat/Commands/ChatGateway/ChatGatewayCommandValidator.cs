using FluentValidation;

namespace MyAIAgent.MainSubSys.Application.Chat.Commands.ChatGateway;

public sealed class ChatGatewayCommandValidator : AbstractValidator<ChatGatewayCommand>
{
    public ChatGatewayCommandValidator()
    {
        RuleFor(x => x.UserMessage)
            .NotEmpty().WithMessage("User message is required.")
            .MaximumLength(4000).WithMessage("User message must not exceed 4000 characters.");
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");
    }
}
