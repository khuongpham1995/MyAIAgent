/**
 * MyAIAgent | McpServer · Application
 *
 * Carries a single webpage URL through the validated MCP reading pipeline.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.McpServer.Application.Search.Queries.ReadWebPage;

public sealed record ReadWebPageQuery(string Url) : IRequest<ResponseModel<WebPageContent>>;
