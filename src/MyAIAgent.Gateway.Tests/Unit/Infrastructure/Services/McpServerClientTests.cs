using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MyAIAgent.Common.Tests;
using MyAIAgent.Gateway.Infrastructure.Services;

namespace MyAIAgent.Gateway.Tests.Unit.Infrastructure.Services;

public sealed class McpServerClientTests
{
    [Fact]
    public async Task ListToolsAsync_WhenServerRequiresHandshake_ShouldDiscoverToolsThroughSdk()
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://mcp.example") };
        var client = new McpServerClient(httpClient, NullLogger<McpServerClient>.Instance);

        // Act
        var result = await client.ListToolsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().ContainSingle(tool => tool.Name == "test_tool" && tool.Description == "Test tool");
        handler.Methods.Should().ContainInOrder("initialize", "tools/list");
        handler.Methods.Should().NotContain("tools/call");
    }

    [Fact]
    public async Task ListToolsAsync_WhenHttpFails_ShouldReturnSafeFailure()
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler(failureStatus: HttpStatusCode.ServiceUnavailable);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://mcp.example") };
        var client = new McpServerClient(httpClient, NullLogger<McpServerClient>.Instance);

        // Act
        var result = await client.ListToolsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().NotContain("mcp.example");
    }

    [Fact]
    public async Task ListToolsAsync_WhenProtocolReturnsError_ShouldReturnSafeFailure()
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler(failToolListing: true);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://mcp.example") };
        var client = new McpServerClient(httpClient, NullLogger<McpServerClient>.Instance);

        // Act
        var result = await client.ListToolsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().NotContain("mcp.example");
    }

    [Fact]
    public async Task ListToolsAsync_WhenCancelled_ShouldPropagateCancellation()
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler(waitForCancellation: true);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://mcp.example") };
        var client = new McpServerClient(httpClient, NullLogger<McpServerClient>.Instance);
        using var cancellation = new CancellationTokenSource();

        // Act
        var pending = client.ListToolsAsync(cancellation.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        var action = async () => await pending;

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
    }
}
