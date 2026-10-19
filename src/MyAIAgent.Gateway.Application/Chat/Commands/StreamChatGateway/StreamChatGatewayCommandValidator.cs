using FluentValidation;
using MyAIAgent.Gateway.Domain.Constants;

namespace MyAIAgent.Gateway.Application.Chat.Commands.StreamChatGateway;

public sealed class StreamChatGatewayCommandValidator : AbstractValidator<StreamChatGatewayCommand>
{
    public const int MaxMessageLength = 2000;

    public StreamChatGatewayCommandValidator()
    {
        RuleFor(x => x.UserMessage)
            .NotEmpty().WithMessage("User message is required.")
            .MaximumLength(MaxMessageLength).WithMessage($"User message must not exceed {MaxMessageLength} characters.");
        RuleFor(x => x.SessionId)
            .Matches(GatewayConstant.SessionIdPattern).WithMessage("Session id is invalid.")
            .When(x => !string.IsNullOrEmpty(x.SessionId));
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");
    }
}
