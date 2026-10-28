/**
 * MyAIAgent | Gateway · Tests
 *
 * Exports and verifies the real Gateway route contract using an isolated host with mocked downstream clients.
 */

using System.Text.Json.Nodes;
using System.Net.Http.Json;
using MyAIAgent.Common.Application.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Gateway.Application;
using MyAIAgent.Gateway.Application.Common.Interfaces;
using MyAIAgent.Gateway.Presentation;
using MyAIAgent.Gateway.Presentation.PublicApi;

namespace MyAIAgent.Gateway.Tests.Integration.Presentation;

public sealed class GatewayOpenApiTests
{
    [Fact]
    public async Task ExportDocument_ShouldWriteOfflineContractArtifact()
    {
        // Arrange / Act
        var document = await GenerateAsync();

        // Assert
        document["openapi"]!.GetValue<string>().Should().Be("3.0.4");
        document["paths"]!["/api/admin/agents"]!["get"]!["responses"]!["200"]!["content"]!["application/json"].Should().NotBeNull();
        var directory = Path.Combine(RepositoryRoot(), "artifacts", "openapi");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "gateway.json"), document.ToJsonString(new() { WriteIndented = true }) + "\n");
    }

    [Fact]
    public async Task Document_WhenComparedWithSnapshot_ShouldMatchCurrentRoutesAndSchemas()
    {
        // Arrange
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "src", "Services", "Gateway", "MyAIAgent.Gateway.Client", "OpenApi", "openapi.json")));

        // Act
        var document = await GenerateAsync();

        // Assert
        JsonNode.DeepEquals(document, expected).Should().BeTrue("update Gateway.Client/OpenApi/openapi.json after intentional API contract changes");
    }

    [Fact]
    public async Task Document_ShouldDescribeDistinctBearerRequirementsAndAnonymousHealth()
    {
        // Arrange / Act
        var document = await GenerateAsync();

        // Assert
        document["components"]!["securitySchemes"]!["EntraBearer"]!["scheme"]!.GetValue<string>().Should().Be("bearer");
        document["components"]!["securitySchemes"]!["VisitorBearer"]!["scheme"]!.GetValue<string>().Should().Be("bearer");
        foreach (var (path, methods) in document["paths"]!.AsObject())
            foreach (var (_, operation) in methods!.AsObject())
            {
                var security = operation!["security"];
                if (path.StartsWith("/health/", StringComparison.Ordinal))
                    (security is null || security.AsArray().Count == 0).Should().BeTrue();
                else
                {
                    var scheme = (path.StartsWith("/api/public/", StringComparison.Ordinal) || path.StartsWith("/sse/public/", StringComparison.Ordinal)) ? "VisitorBearer" : "EntraBearer";
                    security!.AsArray().Single()![scheme].Should().NotBeNull();
                }
            }
    }

    [Fact]
    public async Task Routes_WhenHostedOutsideDevelopment_ShouldRetainAuthorizationPolicies()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["Authentication:Audience"] = "22222222-2222-2222-2222-222222222222",
        });
        RegisterApplication(builder);
        builder.Services.AddGatewaySecurity(builder.Configuration, isDevelopment: false);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGatewayEndpoints();
        await app.StartAsync();

        // Act
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToList();

        // Assert
        foreach (var endpoint in endpoints)
        {
            var path = endpoint.RoutePattern.RawText!;
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy);
            if (path.StartsWith("/health/", StringComparison.Ordinal)) policies.Should().BeEmpty();
            else policies.Should().Contain(path.StartsWith("/api/admin/", StringComparison.Ordinal)
                ? GatewaySecurityExtensions.AdminPolicy : GatewaySecurityExtensions.ChatPolicy);
        }
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        (await client.GetAsync("/api/admin/agents")).StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        await app.StopAsync();
    }

    [Fact]
    public async Task Routes_WhenMigrated_ShouldRemoveOldUrlsAndUpdateCreatedLocation()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        RegisterApplication(builder);
        await using var app = builder.Build();
        app.MapGatewayEndpoints();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        Mock.Get(app.Services.GetRequiredService<IOrchestratorClient>())
            .Setup(value => value.CreateAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<string>.Succeed("a-1"));

        // Act / Assert
        foreach (var (method, path) in new[] { ("POST", "/chat"), ("POST", "/chat/stream"), ("GET", "/admin/agents"), ("GET", "/conversations") })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            using var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
            response.Headers.Location.Should().BeNull();
        }
        using var created = await client.PostAsJsonAsync("/api/admin/agents", new { name = "Test", instructions = "Answer" });
        created.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        created.Headers.Location!.OriginalString.Should().Be("/api/admin/agents/a-1");
    }

    private static async Task<JsonNode> GenerateAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(GatewayEndpoints).Assembly.GetName().Name,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.Sources.Clear();
        RegisterApplication(builder);
        var publicChat = new PublicChatSettings(PublicChatTestApp.Issuer, PublicChatTestApp.Audience, null,
            PublicChatTestApp.SigningKey, [PublicChatTestApp.AllowedOrigin], PublicChatTestApp.ConfiguredAgent,
            TrustForwardedHeaders: false, new PublicChatRateLimits());
        builder.Services.AddPublicChat(publicChat, isDevelopment: true);
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.MapGatewayEndpoints(publicChat);
        app.MapOpenApi();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            return JsonNode.Parse(await client.GetStringAsync("/openapi/kyle-ai-agent.json"))!;
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static void RegisterApplication(WebApplicationBuilder builder)
    {
        builder.Services.AddApplication();
        builder.Services.AddGatewayOpenApi();
        builder.Services.AddGlobalExceptionHandling();
        builder.Services.AddSingleton(new Mock<IOrchestratorClient>().Object);
        builder.Services.AddSingleton(new Mock<IOrchestratorStreamClient>().Object);
        builder.Services.AddSingleton(new Mock<IKernelMemoryClient>().Object);
        builder.Services.AddSingleton(new Mock<IMcpServerClient>().Object);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "package.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
