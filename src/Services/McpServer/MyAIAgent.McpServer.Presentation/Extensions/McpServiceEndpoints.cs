/**
 * MyAIAgent | McpServer · Presentation
 *
 * Maps MCP protocol and anonymous health routes after service-authorization middleware is installed.
 */

namespace MyAIAgent.McpServer.Presentation.Extensions;

public static class McpServiceEndpoints
{
    public static IEndpointRouteBuilder MapMcpServiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok());
        endpoints.MapGet("/health/ready", () => Results.Ok());
        var mcp = endpoints.MapGroup(string.Empty);
        mcp.MapMcp("/mcp");
        // Keep the original root endpoint for stored agent definitions.
        mcp.MapMcp();
        return endpoints;
    }
}
