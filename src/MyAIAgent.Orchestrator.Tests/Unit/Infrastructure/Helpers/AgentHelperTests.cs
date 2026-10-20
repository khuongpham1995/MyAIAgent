/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Checks MCP tool discovery, retained connection lifetime, cancellation, and absent endpoints.
 */

using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.Extensions.Configuration;
using MyAIAgent.Common.Infrastructure.Http;
using MyAIAgent.Common.Tests;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Infrastructure.Helpers;

namespace MyAIAgent.Orchestrator.Tests.Unit.Infrastructure.Helpers;

public sealed class AgentHelperTests
{
    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("https://unapproved.example/mcp")]
    [InlineData("https://mcp.example/admin")]
    public async Task GetAgentToolsAsync_WhenAgentDestinationIsBlocked_ShouldFailBeforeNetwork(string endpoint)
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler();
        using var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var helper = new AgentHelper(client, NullLogger<AgentHelper>.Instance, Settings(), factory.Object);

        // Act
        var action = () => helper.GetAgentToolsAsync(new AgentDefinition { McpServerEndpoint = endpoint }, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<OutboundDestinationBlockedException>();
        handler.Methods.Should().BeEmpty();
        factory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAgentToolsAsync_WhenSdkWrapsDestinationDenial_ShouldPropagateSafeFailure()
    {
        // Arrange
        using var client = new HttpClient(new BlockedHandler());
        var helper = new AgentHelper(client, NullLogger<AgentHelper>.Instance, Settings(), Mock.Of<IHttpClientFactory>());

        // Act
        var action = () => helper.GetAgentToolsAsync(new AgentDefinition { McpServerEndpoint = "https://mcp.example/mcp" }, CancellationToken.None);

        // Assert
        var failure = (await action.Should().ThrowAsync<OutboundDestinationBlockedException>()).Which;
        failure.Reason.Should().Be("dns-address");
        failure.InnerException.Should().BeNull();
    }

    private sealed class BlockedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Sensitive DNS details", new OutboundDestinationBlockedException("dns-address"));
    }

    [Theory]
    [InlineData("https://unapproved.example/messages")]
    [InlineData("https://mcp.example/mcp")]
    [InlineData("http://169.254.169.254/messages")]
    public async Task GetAgentToolsAsync_WhenSseAdvertisesForbiddenDestination_ShouldNotSendOrSilentlyContinue(string advertised)
    {
        // Arrange
        var settings = McpServiceClientSettings.Load(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["McpServer:BaseUrl"] = "https://mcp.example",
            ["McpServer:AccessKey"] = "test-key",
            ["McpServer:AllowedExternalOrigins:0"] = "https://external.example"
        }).Build(), false);
        using var wire = new SseHandler(advertised);
        using var guard = new OutboundRequestHandler(settings.IsAllowedExternalEndpoint, anonymous: true) { InnerHandler = wire };
        using var external = new HttpClient(guard);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(AgentHelper.ExternalClientName)).Returns(external);
        using var trusted = new HttpClient(new McpTestHttpMessageHandler());
        var helper = new AgentHelper(trusted, NullLogger<AgentHelper>.Instance, settings, factory.Object);

        // Act
        var action = () => helper.GetAgentToolsAsync(new AgentDefinition { McpServerEndpoint = "https://external.example/sse" }, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<OutboundDestinationBlockedException>();
        wire.Destinations.Should().OnlyContain(uri => uri.Host == "external.example");
    }

    private sealed class SseHandler(string advertised) : HttpMessageHandler
    {
        public List<Uri> Destinations { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Destinations.Add(request.RequestUri!);
            request.Headers.Authorization.Should().BeNull();
            return Task.FromResult(request.Method == HttpMethod.Get
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent($"event: endpoint\ndata: {advertised}\n\n", System.Text.Encoding.UTF8, "text/event-stream")
                }
                : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }

    private static McpServiceClientSettings Settings() => McpServiceClientSettings.Load(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["McpServer:BaseUrl"] = "https://mcp.example",
            ["McpServer:AccessKey"] = "test-key"
        }).Build(), false);

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task GetAgentToolsAsync_WhenAuthenticationFails_ShouldFailInsteadOfSilentlyDroppingTools(int status)
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler(failureStatus: (System.Net.HttpStatusCode)status);
        using var httpClient = new HttpClient(handler);
        var helper = new AgentHelper(httpClient, NullLogger<AgentHelper>.Instance, Settings(), Mock.Of<IHttpClientFactory>());

        // Act
        var action = () => helper.GetAgentToolsAsync(
            new AgentDefinition { McpServerEndpoint = "https://mcp.example/mcp" }, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetAgentToolsAsync_WhenServerUsesStreamableHttp_ShouldKeepToolsUsableUntilTurnEnds()
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var logger = new Mock<ILogger<AgentHelper>>();
        var helper = new AgentHelper(httpClient, logger.Object, Settings(), Mock.Of<IHttpClientFactory>());
        var agent = new AgentDefinition { McpServerEndpoint = "https://mcp.example/mcp" };

        // Act
        await using var tools = await helper.GetAgentToolsAsync(agent, CancellationToken.None);

        // Assert
        var tool = tools.Tools.Should().ContainSingle("MCP discovery should succeed: {0}",
            string.Join("; ", logger.Invocations.SelectMany(call => call.Arguments))).Which.Should().BeAssignableTo<AIFunction>().Which;

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        // Assert
        result!.ToString().Should().Contain("tool reply");
        handler.Methods.Should().ContainInOrder("initialize", "tools/list", "tools/call");
    }

    [Fact]
    public async Task GetAgentToolsAsync_WhenDiscoveryIsCancelled_ShouldPropagateCancellation()
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler(waitForCancellation: true);
        using var httpClient = new HttpClient(handler);
        var helper = new AgentHelper(httpClient, NullLogger<AgentHelper>.Instance, Settings(), Mock.Of<IHttpClientFactory>());
        using var cancellation = new CancellationTokenSource();

        // Act
        var pending = helper.GetAgentToolsAsync(new AgentDefinition { McpServerEndpoint = "https://mcp.example/mcp" }, cancellation.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        var action = async () => await pending;

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetAgentToolsAsync_WhenEndpointIsEmpty_ShouldAvoidHttpRequests()
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var helper = new AgentHelper(httpClient, NullLogger<AgentHelper>.Instance, Settings(), Mock.Of<IHttpClientFactory>());

        // Act
        await using var tools = await helper.GetAgentToolsAsync(new AgentDefinition(), CancellationToken.None);

        // Assert
        tools.Tools.Should().BeEmpty();
        handler.Methods.Should().BeEmpty();
    }

}
