using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Admin;

namespace MyAIAgent.MainSubSys.Application.Common.Interfaces;

public interface IMcpServerClient
{
    Task<ResponseModel<IReadOnlyList<ToolInfo>>> ListToolsAsync(CancellationToken cancellationToken);
}
