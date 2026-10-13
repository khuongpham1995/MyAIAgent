using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Interfaces;
using MyAIAgent.MainSubSys.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Handlers.Admin;

public sealed class ListMcpToolsHandler(IMcpServerClient mcpServerClient)
    : IRequestHandler<ListMcpToolsQuery, ResponseModel<IReadOnlyList<ToolInfo>>>
{
    public async Task<ResponseModel<IReadOnlyList<ToolInfo>>> Handle(
        ListMcpToolsQuery query,
        CancellationToken cancellationToken) =>
        await mcpServerClient.ListToolsAsync(cancellationToken);
}
