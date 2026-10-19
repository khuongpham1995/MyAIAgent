using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using MyAIAgent.Common.Infrastructure.Extensions;

namespace MyAIAgent.Gateway.Tests.Unit.Infrastructure;

public sealed class AzureAppConfigurationExtensionTests
{
    [Fact]
    public void AddAzureAppConfiguration_WhenDevelopmentEndpointIsAbsent_ShouldAllowMiddleware()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });

        // Act
        builder.AddAzureAppConfiguration();
        using var app = builder.Build();

        var action = () => app.UseAzureAppConfigurationWhenConfigured(app.Configuration);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void AddAzureAppConfiguration_WhenProductionEndpointIsAbsent_ShouldFail()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });

        // Act
        var action = () => builder.AddAzureAppConfiguration();

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("*AppConfiguration:Endpoint*");
    }

    [Theory]
    [InlineData("http://configuration.azconfig.io")]
    [InlineData("not-an-endpoint")]
    [InlineData("https://user:password@configuration.azconfig.io")]
    public void AddAzureAppConfiguration_WhenEndpointIsInvalid_ShouldFailBeforeConnecting(string endpoint)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppConfiguration:Endpoint"] = endpoint,
        });

        // Act
        var action = () => builder.AddAzureAppConfiguration();

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("*AppConfiguration:Endpoint*");
    }
    [Fact]
    public void InsertBeforeLocalOverrides_WhenRemoteSettingsConflict_ShouldPreserveDeploymentSecrets()
    {
        // Arrange
        var folder = Directory.CreateTempSubdirectory("configuration-precedence-");
        var file = Path.Combine(folder.FullName, "appsettings.json");
        File.WriteAllText(file, """{"Model":"default","ApiKey":""}""");
        try
        {
            using var configuration = new ConfigurationManager();
            configuration.AddJsonFile(file);
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ApiKey"] = "deployment-value" });
            var remote = new MemoryConfigurationSource
            {
                InitialData = new Dictionary<string, string?> { ["Model"] = "remote-model", ["ApiKey"] = "remote-value" },
            };

            // Act
            AzureAppConfigurationExtension.InsertBeforeLocalOverrides(configuration, remote);

            // Assert
            configuration["Model"].Should().Be("remote-model");
            configuration["ApiKey"].Should().Be("deployment-value");
        }
        finally
        {
            folder.Delete(true);
        }
    }
    [Theory]
    [InlineData("Other*")]
    [InlineData("Gateway,*:")]
    public void AddAzureAppConfiguration_WhenPrefixContainsFilters_ShouldFailBeforeConnecting(string prefix)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppConfiguration:Endpoint"] = "https://configuration.azconfig.io/",
            ["AppConfiguration:KeyPrefix"] = prefix,
        });

        // Act
        var action = () => builder.AddAzureAppConfiguration();

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("*AppConfiguration:KeyPrefix*");
    }

    [Fact]
    public void AddAzureAppConfiguration_WhenProductionIdentityIsAbsent_ShouldFailBeforeConnecting()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppConfiguration:Endpoint"] = "https://configuration.azconfig.io/",
            ["AzureIdentity:ClientId"] = null,
        });

        // Act
        var action = () => builder.AddAzureAppConfiguration();

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("*AzureIdentity:ClientId*");
    }
}
