using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Application.Common.Interfaces;

namespace MyAIAgent.Gateway.Application.Admin.Queries.ListMcpTools;

public sealed class ListMcpToolsHandler(IMcpServerClient mcpServerClient)
    : IRequestHandler<ListMcpToolsQuery, ResponseModel<IReadOnlyList<ToolInfo>>>
{
    public async Task<ResponseModel<IReadOnlyList<ToolInfo>>> Handle(
        ListMcpToolsQuery query,
        CancellationToken cancellationToken) =>
        await mcpServerClient.ListToolsAsync(cancellationToken);
}
