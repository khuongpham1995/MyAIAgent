/**
 * MyAIAgent | McpServer · Application
 *
 * Carries web search input through the McpServer MediatR application boundary.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.McpServer.Application.Search.Queries.WebSearch;

public record WebSearchQuery(string Query, int Top = 3) : IRequest<ResponseModel<IReadOnlyList<PageContent>>>;
