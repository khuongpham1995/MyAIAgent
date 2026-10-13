using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Interfaces;

public interface IMcpServerClient
{
    Task<ResponseModel<IReadOnlyList<ToolInfo>>> ListToolsAsync(CancellationToken cancellationToken);
}
