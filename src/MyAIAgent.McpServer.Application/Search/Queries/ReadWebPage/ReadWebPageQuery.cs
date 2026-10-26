/**
 * MyAIAgent | McpServer · Application
 *
 * Carries a single webpage URL through the validated MCP reading pipeline.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.McpServer.Application.Search.Queries.ReadWebPage;

public sealed record ReadWebPageQuery(string Url) : IRequest<ResponseModel<WebPageContent>>;
