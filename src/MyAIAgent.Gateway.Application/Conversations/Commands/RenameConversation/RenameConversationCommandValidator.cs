using FluentValidation;
using MyAIAgent.Gateway.Domain.Constants;

namespace MyAIAgent.Gateway.Application.Conversations.Commands.RenameConversation;

public sealed class RenameConversationCommandValidator : AbstractValidator<RenameConversationCommand>
{
    public RenameConversationCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session id is required.")
            .Matches(GatewayConstant.SessionIdPattern).WithMessage("Session id is invalid.");
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(100).WithMessage("Title must not exceed 100 characters.");
    }
}
