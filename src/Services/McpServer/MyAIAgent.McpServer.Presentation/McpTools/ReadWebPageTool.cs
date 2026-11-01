/**
 * MyAIAgent | McpServer · Presentation
 *
 * Exposes webpage reading through MediatR and returns safe MCP errors for unsupported input or extraction failure.
 */

using System.ComponentModel;
using System.Text.Json;
using FluentValidation;
using MediatR;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using MyAIAgent.McpServer.Application.Search.Queries.ReadWebPage;
using MyAIAgent.McpServer.Domain.Constants;

namespace MyAIAgent.McpServer.Presentation.McpTools;

[McpServerToolType]
public sealed class ReadWebPageTool(ISender sender)
{
    [McpServerTool(Name = McpServerConstant.ReadWebPageToolName), Description(McpServerConstant.ReadWebPageToolDescription)]
    public async Task<string> ReadWebPageAsync(
        [Description(McpServerConstant.ReadWebPageUrlPropertyDescription)] string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await sender.Send(new ReadWebPageQuery(url), cancellationToken);
            if (!result.IsSuccess) throw new McpException(result.Message ?? McpServerConstant.ErrorReadWebPageFailed);
            return JsonSerializer.Serialize(result.Data);
        }
        catch (ValidationException)
        {
            throw new McpException("A public HTTPS URL of at most 2048 characters without credentials, IP literals, or a query string is required.");
        }
    }
}
