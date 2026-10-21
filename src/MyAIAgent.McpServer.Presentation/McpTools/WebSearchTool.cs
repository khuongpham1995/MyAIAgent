/**
 * MyAIAgent | McpServer · Presentation
 *
 * Exposes serialized search evidence and translates application failures into safe MCP tool errors.
 */

using System.ComponentModel;
using System.Text.Json;
using FluentValidation;
using MediatR;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using MyAIAgent.McpServer.Application.Search.Queries.WebSearch;
using MyAIAgent.McpServer.Domain.Constants;

namespace MyAIAgent.McpServer.Presentation.McpTools;

[McpServerToolType]
public sealed class WebSearchTool(ISender sender)
{
    [McpServerTool(Name = McpServerConstant.WebSearchToolName),
     Description(McpServerConstant.WebSearchToolDescription)]
    public async Task<string> WebSearchAsync(
        [Description(McpServerConstant.WebSearchQueryPropertyDescription)] string query,
        [Description(McpServerConstant.WebSearchTopPropertyDescription)] int top = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await sender.Send(new WebSearchQuery(query, top), cancellationToken);
            if (!result.IsSuccess) throw new McpException(result.Message ?? McpServerConstant.ErrorSearchFailed);
            return JsonSerializer.Serialize(result.Data);
        }
        catch (ValidationException)
        {
            throw new McpException("Search requires a nonblank query of at most 500 characters and top between 1 and 10.");
        }
    }
}
