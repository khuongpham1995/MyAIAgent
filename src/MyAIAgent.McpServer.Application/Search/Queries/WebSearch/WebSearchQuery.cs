using MediatR;

namespace MyAIAgent.McpServer.Application.Search.Queries.WebSearch;

public record WebSearchQuery(string Query, int Top = 3) : IRequest<string>;
