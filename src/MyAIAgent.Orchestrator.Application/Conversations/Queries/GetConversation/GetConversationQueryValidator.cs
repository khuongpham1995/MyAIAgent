using FluentValidation;

namespace MyAIAgent.Orchestrator.Application.Conversations.Queries.GetConversation;

public sealed class GetConversationQueryValidator : AbstractValidator<GetConversationQuery>
{
    public GetConversationQueryValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session id is required.");
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");
    }
}
