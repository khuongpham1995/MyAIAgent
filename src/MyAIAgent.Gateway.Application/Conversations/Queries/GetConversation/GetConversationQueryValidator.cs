using FluentValidation;
using MyAIAgent.Gateway.Domain.Constants;

namespace MyAIAgent.Gateway.Application.Conversations.Queries.GetConversation;

public sealed class GetConversationQueryValidator : AbstractValidator<GetConversationQuery>
{
    public GetConversationQueryValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session id is required.")
            .Matches(GatewayConstant.SessionIdPattern).WithMessage("Session id is invalid.");
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");
    }
}
