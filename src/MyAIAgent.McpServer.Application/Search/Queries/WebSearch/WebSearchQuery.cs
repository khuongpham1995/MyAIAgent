/**
 * MyAIAgent | McpServer · Application
 *
 * Carries web search and page retrieval input through the McpServer MediatR application boundary.
 */

using MediatR;

namespace MyAIAgent.McpServer.Application.Search.Queries.WebSearch;

public record WebSearchQuery(string Query, int Top = 3) : IRequest<string>;
