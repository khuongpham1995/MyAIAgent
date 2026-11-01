/**
 * MyAIAgent | Gateway · Application
 *
 * Defines the Gateway application port for discovering MCP tools without depending on the protocol transport.
 */

using MyAIAgent.Common.Application.Models;
using MyAIAgent.Gateway.Application.Admin;

namespace MyAIAgent.Gateway.Application.Common.Interfaces;

public interface IMcpServerClient
{
    Task<ResponseModel<IReadOnlyList<ToolInfo>>> ListToolsAsync(CancellationToken cancellationToken);
}
