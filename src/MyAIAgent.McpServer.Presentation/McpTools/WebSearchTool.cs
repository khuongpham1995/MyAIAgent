using MediatR;
using ModelContextProtocol.Server;
using MyAIAgent.McpServer.Application.Handlers.Search;
using MyAIAgent.McpServer.Domain.Constants;
using System.ComponentModel;

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
        => await sender.Send(new WebSearchQuery(query, top), cancellationToken);
}
