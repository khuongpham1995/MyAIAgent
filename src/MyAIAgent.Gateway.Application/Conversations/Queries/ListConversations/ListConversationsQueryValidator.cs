using FluentValidation;

namespace MyAIAgent.Gateway.Application.Conversations.Queries.ListConversations;

public sealed class ListConversationsQueryValidator : AbstractValidator<ListConversationsQuery>
{
    public const int MaxPageSize = 50;

    public ListConversationsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize).WithMessage($"Page size must be between 1 and {MaxPageSize}.");
    }
}
