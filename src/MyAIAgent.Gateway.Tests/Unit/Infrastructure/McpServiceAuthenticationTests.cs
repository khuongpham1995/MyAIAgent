/**
 * MyAIAgent | Gateway · Tests
 *
 * Verifies trusted MCP credential boundaries, configuration rejection, and redirect isolation.
 */

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Common.Infrastructure.Http;

namespace MyAIAgent.Gateway.Tests.Unit.Infrastructure;

public sealed class McpServiceAuthenticationTests
{
    private const string Key = "test-mcp-service-key";

    private static McpServiceClientSettings Settings(string? origin = "https://mcp.example", string? key = Key, bool development = false) =>
        McpServiceClientSettings.Load(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["McpServer:BaseUrl"] = origin,
            ["McpServer:AccessKey"] = key
        }).Build(), development);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://mcp.example")]
    [InlineData("https://user:password@mcp.example")]
    [InlineData("https://mcp.example/mcp")]
    [InlineData("https://mcp.example?query=value")]
    [InlineData("https://mcp.example#fragment")]
    [InlineData("file:///tmp/mcp")]
    public void Settings_WhenTrustedOriginIsInvalid_ShouldRejectIt(string? origin)
    {
        // Arrange & Act
        var action = () => Settings(origin);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("McpServer:BaseUrl*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad\r\nheader")]
    public void Settings_WhenProductionKeyIsInvalid_ShouldRejectWithoutPrintingIt(string? key)
    {
        // Arrange & Act
        var action = () => Settings(key: key);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("McpServer:AccessKey*");
    }

    [Theory]
    [InlineData("http://localhost:5255", true)]
    [InlineData("http://127.0.0.1:5255", true)]
    [InlineData("http://[::1]:5255", true)]
    [InlineData("http://mcp.example", false)]
    public void Settings_WhenDevelopmentUsesHttp_ShouldPermitOnlyLoopback(string origin, bool allowed)
    {
        // Arrange & Act
        var action = () => Settings(origin, key: null, development: true);

        // Assert
        if (allowed) action.Should().NotThrow();
        else action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("https://mcp.example/", true)]
    [InlineData("https://mcp.example/mcp", true)]
    [InlineData("https://MCP.example:443/mcp/", true)]
    [InlineData("https://other.example/mcp", false)]
    [InlineData("https://mcp.example.evil.test/mcp", false)]
    [InlineData("https://mcp.example:444/mcp", false)]
    [InlineData("http://mcp.example/mcp", false)]
    [InlineData("https://mcp.example/other", false)]
    [InlineData("https://mcp.example/mcp/other", false)]
    [InlineData("https://mcp.example/MCP", false)]
    [InlineData("https://mcp.example/mcp?session=other", false)]
    [InlineData("https://user@mcp.example/mcp", false)]
    [InlineData("https://mcp.example/mcp#fragment", false)]
    public async Task SendAsync_WhenDestinationVaries_ShouldSendKeyOnlyToTrustedProtocolEndpoints(string endpoint, bool trusted)
    {
        // Arrange
        using var capture = new CaptureHandler();
        using var handler = new McpServiceKeyHandler(Settings()) { InnerHandler = capture };
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("Authorization", "must-not-leak");

        // Act
        using var response = await client.GetAsync(endpoint);

        // Assert
        capture.Key.Should().Be(trusted ? Key : null);
        capture.RequestCount.Should().Be(1);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public async Task SendAsync_WhenProtocolUsesDifferentMethods_ShouldAuthenticateEveryRequest(string method)
    {
        // Arrange
        using var capture = new CaptureHandler();
        using var handler = new McpServiceKeyHandler(Settings()) { InnerHandler = capture };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(new HttpMethod(method), "https://mcp.example/mcp");

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        capture.Key.Should().Be(Key);
    }

    [Fact]
    public async Task SendAsync_WhenHostIsOverridden_ShouldRejectBeforeSendingCredential()
    {
        // Arrange
        using var capture = new CaptureHandler();
        using var handler = new McpServiceKeyHandler(Settings()) { InnerHandler = capture };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://mcp.example/mcp");
        request.Headers.Host = "other.example";

        // Act
        var action = () => client.SendAsync(request);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
        capture.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task FactoryClient_WhenServerRedirects_ShouldNotFollowOrForwardCredential()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        var sinkRequests = 0;
        string? sourceKey = null;
        app.MapGet("/mcp", (HttpContext context) =>
        {
            sourceKey = context.Request.Headers.Authorization;
            return Results.Redirect("/sink");
        });
        app.MapGet("/sink", () => { sinkRequests++; return Results.Ok(); });
        await app.StartAsync();
        var settings = Settings(app.Urls.Single(), development: true);
        var services = new ServiceCollection();
        services.AddHttpClient("mcp").AddMcpServiceAuthentication(settings);
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("mcp");

        // Act
        using var response = await client.GetAsync(new Uri(settings.Origin, "/mcp"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        sourceKey.Should().Be(Key);
        sinkRequests.Should().Be(0);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Key { get; private set; }
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Key = request.Headers.TryGetValues("Authorization", out var values) ? values.Single() : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
