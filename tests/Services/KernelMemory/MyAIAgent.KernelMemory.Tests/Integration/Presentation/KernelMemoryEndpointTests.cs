/**
 * MyAIAgent | KernelMemory · Tests
 *
 * Verifies daemon route composition, anonymous health checks, and authorization before any memory-provider access.
 */

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Context;
using Moq;
using MyAIAgent.KernelMemory.Presentation.Extensions;

namespace MyAIAgent.KernelMemory.Tests.Integration.Presentation;

public sealed class KernelMemoryEndpointTests
{
    [Fact]
    public async Task MapKernelMemoryEndpoints_WhenWebServiceDisabled_ShouldNotRegisterRoutes()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        await using var app = builder.Build();
        var config = new KernelMemoryConfig();
        config.Service.RunWebService = false;

        // Act
        app.MapKernelMemoryEndpoints(config, DateTimeOffset.UnixEpoch);

        // Assert
        ((IEndpointRouteBuilder)app).DataSources.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("wrong-key", HttpStatusCode.Forbidden)]
    public async Task DaemonModule_WhenKeyMissingOrInvalid_ShouldRejectBeforeProviderAccess(string? key, HttpStatusCode expected)
    {
        // Arrange
        var memory = new Mock<IKernelMemory>();
        await using var app = await StartAsync(memory.Object);
        using var client = CreateClient(app);
        if (key is not null) client.DefaultRequestHeaders.Add("Authorization", key);

        // Act
        using var response = await client.GetAsync("/upload-status?documentId=test-document");

        // Assert
        response.StatusCode.Should().Be(expected);
        memory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DaemonModule_WhenNoKey_ShouldKeepStatusAndHealthAnonymous()
    {
        // Arrange
        var memory = new Mock<IKernelMemory>();
        await using var app = await StartAsync(memory.Object);
        using var client = CreateClient(app);

        // Act / Assert
        foreach (var path in new[] { "/", "/health", "/health/live", "/health/ready" })
        {
            using var response = await client.GetAsync(path);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        memory.VerifyNoOtherCalls();
    }

    private static async Task<WebApplication> StartAsync(IKernelMemory memory)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(memory);
        builder.Services.AddSingleton(Mock.Of<IContextProvider>());
        var config = new KernelMemoryConfig();
        config.Service.RunWebService = true;
        config.ServiceAuthorization.Enabled = true;
        config.ServiceAuthorization.AccessKey1 = "test-access-key-1-abcdefghijklmnop";
        config.ServiceAuthorization.AccessKey2 = "test-access-key-2-abcdefghijklmnop";
        var app = builder.Build();
        app.MapKernelMemoryEndpoints(config, DateTimeOffset.UnixEpoch);
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app) => new()
    {
        BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single()),
    };
}
