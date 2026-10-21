/**
 * MyAIAgent | McpServer · Tests
 *
 * Exercises the MCP HTTP endpoint and tool invocation through a real in-process protocol server.
 */

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using MyAIAgent.McpServer.Application;
using MyAIAgent.McpServer.Application.Common.Interfaces;
using MyAIAgent.McpServer.Application.Search;
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
using MyAIAgent.McpServer.Presentation.Extensions;

namespace MyAIAgent.McpServer.Tests.Integration.Presentation;

public sealed class McpTransportTests
{
    [Theory]
    [InlineData("/mcp", "test-primary-key")]
    [InlineData("/", "test-primary-key")]
    [InlineData("/mcp", "test-rotation-key")]
    [InlineData("/", "test-rotation-key")]
    public async Task Mcp_WhenAuthenticated_ShouldDiscoverAndInvokeToolOverHttp(string endpoint, string key)
    {
        // Arrange
        await using var app = await StartServerAsync();
        using var httpClient = app.GetTestClient();
        await using var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("http://localhost" + endpoint),
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = key }
        }, httpClient);
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: CancellationToken.None);

        // Act
        var tools = await client.ListToolsAsync(cancellationToken: CancellationToken.None);
        var result = await client.CallToolAsync(McpServerConstant.GetCurrentDateTimeToolName, cancellationToken: CancellationToken.None);

        // Assert
        tools.Should().Contain(tool => tool.Name == McpServerConstant.GetCurrentDateTimeToolName);
        result.IsError.Should().NotBeTrue();
        DateTimeOffset.TryParse(result.Content.OfType<TextContentBlock>().Single().Text, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("/mcp", "GET", null)]
    [InlineData("/mcp", "DELETE", null)]
    [InlineData("/", "GET", null)]
    [InlineData("/", "DELETE", null)]
    [InlineData("/mcp", "GET", "wrong-key")]
    [InlineData("/mcp", "DELETE", "wrong-key")]
    [InlineData("/", "GET", "wrong-key")]
    [InlineData("/", "DELETE", "wrong-key")]
    public async Task Mcp_WhenSessionRequestIsUnauthorized_ShouldRejectIt(string endpoint, string method, string? key)
    {
        // Arrange
        await using var app = await StartServerAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        request.Headers.Add("Accept", "text/event-stream");
        request.Headers.Add("Mcp-Session-Id", "untrusted-session");
        if (key is not null) request.Headers.Add("Authorization", key);

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(key is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_WhenAnonymous_ShouldRemainAccessible(string path)
    {
        // Arrange
        await using var app = await StartServerAsync();
        using var client = app.GetTestClient();

        // Act
        using var response = await client.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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

    [Theory]
    [InlineData("/mcp", null)]
    [InlineData("/", null)]
    [InlineData("/mcp", "wrong-key")]
    [InlineData("/", "wrong-key")]
    public async Task Mcp_WhenToolCallIsUnauthorized_ShouldNotExecuteTheTool(string endpoint, string? key)
    {
        // Arrange
        var clock = new CountingClock();
        await using var app = await StartServerAsync(clock);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
        if (key is not null) client.DefaultRequestHeaders.Add("Authorization", key);

        // Act
        using var response = await client.PostAsJsonAsync(endpoint, new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new { name = McpServerConstant.GetCurrentDateTimeToolName, arguments = new { } }
        });

        // Assert
        response.StatusCode.Should().Be(key is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
        clock.Reads.Should().Be(0);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("empty")]
    [InlineData("failure")]
    [InlineData("invalid")]
    public async Task Search_WhenInvokedOverHttp_ShouldReturnEvidenceOrExplicitToolError(string outcome)
    {
        // Arrange
        var search = new Mock<ISearchProvider>();
        search.Setup(p => p.SearchAsync("focused query", 3, It.IsAny<CancellationToken>())).ReturnsAsync(outcome == "failure"
            ? ResponseModel<IReadOnlyList<SearchEntry>>.Fail(message: "Web search quota or rate limit was reached.")
            : ResponseModel<IReadOnlyList<SearchEntry>>.Succeed(outcome == "empty" ? [] : [new("https://example.com/", "Title", "Excerpt")]));
        await using var app = await StartServerAsync(search: search.Object);
        using var http = app.GetTestClient();
        await using var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("http://localhost/mcp"),
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "test-primary-key" }
        }, http);
        await using var client = await McpClient.CreateAsync(transport);
        var tools = await client.ListToolsAsync();

        // Act
        var result = await client.CallToolAsync(McpServerConstant.WebSearchToolName,
            new Dictionary<string, object?> { ["query"] = outcome == "invalid" ? "" : "focused query" });

        // Assert
        tools.Should().Contain(t => t.Name == "web_search");
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        if (outcome is "failure" or "invalid")
        {
            result.IsError.Should().BeTrue();
            text.Should().Contain(outcome == "failure" ? "quota" : "500");
        }
        else
        {
            result.IsError.Should().NotBeTrue();
            using var json = JsonDocument.Parse(text);
            json.RootElement.GetArrayLength().Should().Be(outcome == "empty" ? 0 : 1);
            if (outcome == "success") json.RootElement[0].GetProperty("Content").GetString().Should().Be("Excerpt");
        }
        search.Verify(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            outcome == "invalid" ? Times.Never : Times.Once);
    }

    private sealed class CountingClock : TimeProvider
    {
        public int Reads { get; private set; }
        public override DateTimeOffset GetUtcNow()
        {
            Reads++;
            return DateTimeOffset.UnixEpoch;
        }
    }

    private static async Task<WebApplication> StartServerAsync(TimeProvider? clock = null, ISearchProvider? search = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(clock ?? TimeProvider.System);
        builder.Services.AddSingleton(Options.Create(new ServiceAuthorizationModel
        {
            Enabled = true,
            HttpHeaderName = "Authorization",
            AccessKey1 = "test-primary-key",
            AccessKey2 = "test-rotation-key"
        }));
        builder.Services.AddSingleton<HttpAuthEndpointFilter>();
        builder.Services.AddApplication();
        builder.Services.AddSingleton(search ?? new Mock<ISearchProvider>().Object);
        builder.Services.AddMcpServer().WithHttpTransport().WithTools<DateTimeTool>().WithTools<WebSearchTool>();
        var app = builder.Build();
        app.MapMcpServiceEndpoints();
        await app.StartAsync();
        return app;
    }
}
