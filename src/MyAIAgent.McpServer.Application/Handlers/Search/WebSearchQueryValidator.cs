using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MyAIAgent.McpServer.Application.Interfaces;
using MyAIAgent.McpServer.Domain.Models;

namespace MyAIAgent.McpServer.Application.Handlers.Search;

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
