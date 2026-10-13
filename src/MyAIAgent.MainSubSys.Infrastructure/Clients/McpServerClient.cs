using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Domain.Constants;
using MyAIAgent.MainSubSys.Application.Interfaces;
using MyAIAgent.MainSubSys.Domain.Models;

namespace MyAIAgent.MainSubSys.Infrastructure.Clients;

public sealed class McpServerClient(
    HttpClient httpClient,
    ILogger<McpServerClient> logger) : IMcpServerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ResponseModel<IReadOnlyList<ToolInfo>>> ListToolsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/list",
                @params = new { }
            };

            var response = await httpClient.PostAsJsonAsync("/mcp", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<McpToolsListResult>(
                JsonOptions,
                cancellationToken);

            var tools = result?.Result?.Tools
                ?.Select(t => new ToolInfo(t.Name ?? string.Empty, t.Description ?? string.Empty))
                .ToList()
                ?? [];

            return ResponseModel<IReadOnlyList<ToolInfo>>.Succeed(tools);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "MCP Server tool listing failed");
            return ResponseModel<IReadOnlyList<ToolInfo>>.Fail(message: MainSubSysConstant.Errors.ToolListFailed);
        }
    }

    private sealed record McpToolsListResult(
        [property: JsonPropertyName("result")] McpToolsResult? Result);

    private sealed record McpToolsResult(
        [property: JsonPropertyName("tools")] List<McpToolEntry>? Tools);

    private sealed record McpToolEntry(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("description")] string? Description);
}
