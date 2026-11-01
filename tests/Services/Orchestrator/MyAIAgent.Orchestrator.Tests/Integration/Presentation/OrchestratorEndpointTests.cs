/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Exercises composed HTTP modules with real authorization and MediatR, replacing persistence at its boundary.
 */

using MyAIAgent.Common.Infrastructure.Configuration;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using MyAIAgent.Common.Infrastructure.Filters;
using MyAIAgent.Orchestrator.Application;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Presentation.Endpoints;

namespace MyAIAgent.Orchestrator.Tests.Integration.Presentation;

public sealed class OrchestratorEndpointTests
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("wrong-key", HttpStatusCode.Forbidden)]
    public async Task Modules_WhenServiceKeyMissingOrInvalid_ShouldProtectEveryApplicationRoute(string? key, HttpStatusCode expected)
    {
        // Arrange
        var repository = new Mock<IAgentRepository>();
        await using var app = await StartAsync(repository.Object);
        using var client = CreateClient(app);
        if (key is not null) client.DefaultRequestHeaders.Add("Authorization", key);
        (string Method, string Path, string? Body)[] requests =
        [
            ("POST", "/api/chat", """{"userId":"owner","userMessage":"Hello"}"""),
            ("POST", "/sse/chat", """{"userId":"owner","userMessage":"Hello"}"""),
            ("GET", "/api/conversations?userId=owner", null),
            ("GET", "/api/conversations/s-1?userId=owner", null),
            ("PUT", "/api/conversations/s-1/title", """{"userId":"owner","title":"Renamed"}"""),
            ("DELETE", "/api/conversations/s-1?userId=owner", null),
            ("GET", "/api/agents", null),
            ("POST", "/api/agents", """{"name":"Test","instructions":"Answer clearly","enabledTools":[]}"""),
            ("DELETE", "/api/agents/a-1", null),
        ];

        // Act / Assert
        foreach (var (method, path, body) in requests)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(expected, $"{method} {path} must retain the service filter");
        }
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("test-primary-key")]
    [InlineData("test-rotation-key")]
    public async Task AgentModule_WhenEitherServiceKeyValid_ShouldReachPersistence(string key)
    {
        // Arrange
        var repository = new Mock<IAgentRepository>();
        repository.Setup(value => value.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<AgentDefinition>());
        await using var app = await StartAsync(repository.Object);
        using var client = CreateClient(app);
        client.DefaultRequestHeaders.Add("Authorization", key);

        // Act
        using var response = await client.GetAsync("/api/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        repository.Verify(value => value.ListAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HealthModule_WhenNoServiceKey_ShouldRemainAnonymous()
    {
        // Arrange
        var repository = new Mock<IAgentRepository>();
        await using var app = await StartAsync(repository.Object);
        using var client = CreateClient(app);

        // Act / Assert
        foreach (var path in new[] { "/health/live", "/health/ready" })
        {
            using var response = await client.GetAsync(path);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Modules_WhenOldUrlsRequested_ShouldReturnNotFoundWithoutRedirecting()
    {
        // Arrange
        var repository = new Mock<IAgentRepository>();
        await using var app = await StartAsync(repository.Object);
        using var client = CreateClient(app);
        client.DefaultRequestHeaders.Add("Authorization", "test-primary-key");

        // Act / Assert
        foreach (var (method, path) in new[] { ("POST", "/chat"), ("POST", "/chat/stream"), ("GET", "/agents"), ("GET", "/conversations") })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            using var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            response.Headers.Location.Should().BeNull();
        }
        repository.VerifyNoOtherCalls();
    }

    private static async Task<WebApplication> StartAsync(IAgentRepository repository)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApplication();
        builder.Services.AddSingleton(repository);
        builder.Services.AddSingleton(Options.Create(new ServiceAuthorizationModel
        {
            Enabled = true,
            AccessKey1 = "test-primary-key",
            AccessKey2 = "test-rotation-key",
        }));
        builder.Services.AddSingleton<HttpAuthEndpointFilter>();
        var app = builder.Build();
        app.MapOrchestratorEndpoints();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app) => new()
    {
        BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single()),
    };
}
