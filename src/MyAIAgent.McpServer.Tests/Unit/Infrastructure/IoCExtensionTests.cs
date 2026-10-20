/**
 * MyAIAgent | McpServer · Tests
 *
 * Verifies startup rejects disabled production authorization and incomplete service-key configuration.
 */

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MyAIAgent.McpServer.Infrastructure;

namespace MyAIAgent.McpServer.Tests.Unit.Infrastructure;

public sealed class IoCExtensionTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void AddInfrastructure_WhenAuthorizationIsDisabledOutsideDevelopment_ShouldFail(string environment)
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
        builder.Configuration["ServiceAuthorization:Enabled"] = "false";

        // Act
        var action = () => builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("ServiceAuthorization:Enabled*");
    }

    [Theory]
    [InlineData(null, "rotation")]
    [InlineData("", "rotation")]
    [InlineData("primary", " ")]
    [InlineData("primary", null)]
    public async Task StartAsync_WhenEnabledKeysAreIncomplete_ShouldFailBeforeServing(string? primary, string? rotation)
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServiceAuthorization:Enabled"] = "true",
            ["ServiceAuthorization:AccessKey1"] = primary,
            ["ServiceAuthorization:AccessKey2"] = rotation
        });
        builder.Services.AddInfrastructure(builder.Configuration, isDevelopment: false);
        using var host = builder.Build();

        // Act
        var action = () => host.StartAsync();

        // Assert
        await action.Should().ThrowAsync<OptionsValidationException>();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task StartAsync_WhenConfiguredOrExplicitlyDisabledInDevelopment_ShouldStart(bool enabled, bool development)
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServiceAuthorization:Enabled"] = enabled.ToString(),
            ["ServiceAuthorization:AccessKey1"] = "primary",
            ["ServiceAuthorization:AccessKey2"] = "rotation"
        });
        builder.Services.AddInfrastructure(builder.Configuration, development);
        using var host = builder.Build();

        // Act
        await host.StartAsync();
        await host.StopAsync();

        // Assert
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested.Should().BeTrue();
    }
}
