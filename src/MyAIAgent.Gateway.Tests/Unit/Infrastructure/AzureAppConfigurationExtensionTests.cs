using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using MyAIAgent.Common.Infrastructure.Extensions;

namespace MyAIAgent.Gateway.Tests.Unit.Infrastructure;

public sealed class AzureAppConfigurationExtensionTests
{
    [Fact]
    public void AddAzureAppConfiguration_WhenConnectionStringIsAbsent_ShouldAllowMiddleware()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddAzureAppConfiguration();
        using var app = builder.Build();

        var action = () => app.UseAzureAppConfigurationWhenConfigured(app.Configuration);

        action.Should().NotThrow();
    }
}
