/**
 * MyAIAgent | McpServer · Tests
 *
 * Exercises the MCP HTTP endpoint and tool invocation through a real in-process protocol server.
 */

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Common.Infrastructure.Filters;
using MyAIAgent.McpServer.Domain.Constants;
using MyAIAgent.McpServer.Presentation.McpTools;

namespace MyAIAgent.McpServer.Tests.Integration.Presentation;

public sealed class McpTransportTests
{
    [Theory]
    [InlineData("/mcp")]
    [InlineData("/")]
    public async Task Mcp_WhenAuthenticated_ShouldDiscoverAndInvokeToolOverHttp(string endpoint)
    {
        // Arrange
        await using var app = await StartServerAsync();
        using var httpClient = app.GetTestClient();
        await using var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("http://localhost" + endpoint),
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "test-primary-key" }
        }, httpClient);
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: CancellationToken.None);

        // Act
        var tools = await client.ListToolsAsync(cancellationToken: CancellationToken.None);
        var result = await client.CallToolAsync(McpServerConstant.GetCurrentDateTimeToolName, cancellationToken: CancellationToken.None);

        // Assert
        tools.Should().ContainSingle(tool => tool.Name == McpServerConstant.GetCurrentDateTimeToolName);
        result.IsError.Should().NotBeTrue();
        DateTimeOffset.TryParse(result.Content.OfType<TextContentBlock>().Single().Text, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("/mcp", null, HttpStatusCode.Unauthorized)]
    [InlineData("/mcp", "wrong-key", HttpStatusCode.Forbidden)]
    [InlineData("/", null, HttpStatusCode.Unauthorized)]
    [InlineData("/", "wrong-key", HttpStatusCode.Forbidden)]
    public async Task Mcp_WhenServiceKeyIsMissingOrInvalid_ShouldRejectBeforeProtocolDispatch(string endpoint, string? key, HttpStatusCode expected)
    {
        // Arrange
        await using var app = await StartServerAsync();
        using var httpClient = app.GetTestClient();
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
        if (key is not null)
            httpClient.DefaultRequestHeaders.Add("Authorization", key);

        // Act
        using var response = await httpClient.PostAsJsonAsync(endpoint, new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new { protocolVersion = "2025-11-25", capabilities = new { }, clientInfo = new { name = "test", version = "1" } }
        });

        // Assert
        response.StatusCode.Should().Be(expected);
    }

    private static async Task<WebApplication> StartServerAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(Options.Create(new ServiceAuthorizationModel
        {
            Enabled = true,
            HttpHeaderName = "Authorization",
            AccessKey1 = "test-primary-key",
            AccessKey2 = "test-rotation-key"
        }));
        builder.Services.AddSingleton<HttpAuthEndpointFilter>();
        builder.Services.AddMcpServer().WithHttpTransport().WithTools<DateTimeTool>();
        var app = builder.Build();
        var mcp = app.MapGroup(string.Empty).AddEndpointFilter(app.Services.GetRequiredService<HttpAuthEndpointFilter>());
        mcp.MapMcp("/mcp");
        mcp.MapMcp();
        await app.StartAsync();
        return app;
    }
}
