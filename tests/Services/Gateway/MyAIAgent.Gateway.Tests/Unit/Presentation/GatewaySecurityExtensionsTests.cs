/**
 * MyAIAgent | Gateway · Tests
 *
 * Checks deployed JWT/role configuration and Development authentication behavior.
 */

using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Gateway.Presentation;

namespace MyAIAgent.Gateway.Tests.Unit.Presentation;

public sealed class GatewaySecurityExtensionsTests
{
    [Theory]
    [InlineData("Chat.Access", true, false)]
    [InlineData("Agent.Admin", true, true)]
    [InlineData("Unassigned", false, false)]
    public async Task AuthorizeAsync_WhenRoleIsAssigned_ShouldApplyGatewayPolicies(
        string role,
        bool canChat,
        bool canAdmin)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGatewaySecurity(Configuration(), isDevelopment: false);
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("roles", role), new Claim("tid", "tenant"), new Claim("oid", "user")],
            "test", "name", "roles"));

        // Act
        var chat = await authorization.AuthorizeAsync(principal, GatewaySecurityExtensions.ChatPolicy);
        var admin = await authorization.AuthorizeAsync(principal, GatewaySecurityExtensions.AdminPolicy);

        // Assert
        chat.Succeeded.Should().Be(canChat);
        admin.Succeeded.Should().Be(canAdmin);
    }

    [Fact]
    public void AddGatewaySecurity_WhenProductionConfigurationIsMissing_ShouldFail()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var action = () => services.AddGatewaySecurity(new ConfigurationBuilder().Build(), isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenUserIsAnonymous_ShouldRejectBothPolicies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGatewaySecurity(Configuration(), isDevelopment: false);
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var chat = await authorization.AuthorizeAsync(principal, GatewaySecurityExtensions.ChatPolicy);
        var admin = await authorization.AuthorizeAsync(principal, GatewaySecurityExtensions.AdminPolicy);

        // Assert
        chat.Succeeded.Should().BeFalse();
        admin.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenIdentityClaimsAreMissing_ShouldRejectChat()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGatewaySecurity(Configuration(), isDevelopment: false);
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("roles", "Chat.Access")], "test", "name", "roles"));

        // Act
        var result = await authorization.AuthorizeAsync(principal, GatewaySecurityExtensions.ChatPolicy);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void AddGatewaySecurity_WhenAudienceIsAppIdUri_ShouldRejectConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:TenantId"] = "00000000-0000-0000-0000-000000000001",
                ["Authentication:Audience"] = "api://00000000-0000-0000-0000-000000000002"
            })
            .Build();

        // Act
        var action = () => services.AddGatewaySecurity(configuration, isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:TenantId"] = "00000000-0000-0000-0000-000000000001",
            ["Authentication:Audience"] = "00000000-0000-0000-0000-000000000002"
        })
        .Build();
}
