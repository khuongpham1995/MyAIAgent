/**
 * MyAIAgent | McpServer · Application
 *
 * Rejects unsupported URL syntax before webpage reading reaches the provider boundary.
 */

using FluentValidation;

namespace MyAIAgent.McpServer.Application.Search.Queries.ReadWebPage;

public sealed class ReadWebPageQueryValidator : AbstractValidator<ReadWebPageQuery>
{
    public ReadWebPageQueryValidator()
    {
        RuleFor(query => query.Url).Must(value => WebPageUrl.TryNormalize(value, out _))
            .WithMessage("A public HTTPS URL of at most 2048 characters without credentials, IP literals, or a query string is required.");
    }
}
