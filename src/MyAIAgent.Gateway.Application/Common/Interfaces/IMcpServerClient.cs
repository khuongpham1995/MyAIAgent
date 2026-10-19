using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Application.Admin;

namespace MyAIAgent.Gateway.Application.Common.Interfaces;

public interface IMcpServerClient
{
    Task<ResponseModel<IReadOnlyList<ToolInfo>>> ListToolsAsync(CancellationToken cancellationToken);
}
