using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using MyAIAgent.Common.Infrastructure.Extensions;

namespace MyAIAgent.Gateway.Tests.Integration.Infrastructure;

public sealed class ExceptionHandlingStartupTests
{
    [Fact]
    public async Task StartAsync_WhenExceptionHandlerIsRegistered_ShouldStartSuccessfully()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddGlobalExceptionHandling();
        await using var app = builder.Build();
        app.UseExceptionHandler();

        await app.StartAsync();

        await app.StopAsync();
    }
}
