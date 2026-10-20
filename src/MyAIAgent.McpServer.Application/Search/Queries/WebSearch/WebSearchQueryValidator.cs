/**
 * MyAIAgent | McpServer · Application
 *
 * Validates Query, Top before web search and page retrieval reaches the McpServer handler.
 */

using FluentValidation;

namespace MyAIAgent.McpServer.Application.Search.Queries.WebSearch;

public sealed class WebSearchQueryValidator : AbstractValidator<WebSearchQuery>
{
    public WebSearchQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query is required.");

        RuleFor(x => x.Top)
            .InclusiveBetween(1, 10)
            .WithMessage("Top must be between 1 and 10.");
    }
}
