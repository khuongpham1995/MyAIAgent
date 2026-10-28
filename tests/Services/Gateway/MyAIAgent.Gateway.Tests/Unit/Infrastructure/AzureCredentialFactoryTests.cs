/**
 * MyAIAgent | Gateway · Tests
 *
 * Checks Azure identity selection and rejection of unsafe or malformed service endpoints.
 */

using Azure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MyAIAgent.Common.Infrastructure.Extensions;

namespace MyAIAgent.Gateway.Tests.Unit.Infrastructure;

public sealed class AzureCredentialFactoryTests
{
    [Fact]
    public void Create_WhenDevelopment_ShouldUseDeveloperSignIn()
    {
        // Arrange, Act & Assert
        AzureCredentialFactory.Create(new ConfigurationBuilder().Build(), true).Should().BeOfType<AzureCliCredential>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public void Create_WhenDeployedIdentityIsMissingOrInvalid_ShouldFail(string? clientId)
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureIdentity:ClientId"] = clientId,
        }).Build();

        // Act
        var action = () => AzureCredentialFactory.Create(configuration, false);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("*AzureIdentity:ClientId*");
    }

    [Fact]
    public void Create_WhenDeployedIdentityIsConfigured_ShouldUseOnlyManagedIdentity()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureIdentity:ClientId"] = "11111111-1111-1111-1111-111111111111",
        }).Build();

        // Act & Assert
        AzureCredentialFactory.Create(configuration, false).Should().BeOfType<ManagedIdentityCredential>();
    }
}
