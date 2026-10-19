using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MyAIAgent.Gateway.Presentation.PublicApi;

namespace MyAIAgent.Gateway.Tests.Unit.Presentation;

public sealed class PublicChatSettingsTests
{
    private static readonly string ValidKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static (string, string?)[] ValidHs256(params (string, string?)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["PublicChat:Jwt:Issuer"] = "https://issuer.test",
            ["PublicChat:Jwt:Audience"] = "chat-api",
            ["PublicChat:Jwt:SigningKey"] = ValidKey,
            ["PublicChat:AgentId"] = "agent-1",
            ["PublicChat:AllowedOrigins:0"] = "https://app.example.test",
        };
        foreach (var (key, value) in overrides)
            values[key] = value;

        return values.Select(pair => (pair.Key, pair.Value)).ToArray();
    }

    [Fact]
    public void FromConfiguration_WhenNoJwtSettingIsPresent_ShouldDisablePublicChat()
    {
        // Arrange & Act
        var settings = PublicChatSettings.FromConfiguration(Config(), isDevelopment: false);

        // Assert
        settings.Should().BeNull();
    }

    [Fact]
    public void FromConfiguration_WhenOnlyEmptyPlaceholdersArePresent_ShouldDisablePublicChat()
    {
        // Arrange & Act
        var settings = PublicChatSettings.FromConfiguration(
            Config(("PublicChat:Jwt:Issuer", ""), ("PublicChat:Jwt:SigningKey", "")), isDevelopment: false);

        // Assert
        settings.Should().BeNull();
    }

    [Fact]
    public void FromConfiguration_WhenHs256ConfigurationIsComplete_ShouldReturnSettings()
    {
        // Arrange & Act
        var settings = PublicChatSettings.FromConfiguration(Config(ValidHs256()), isDevelopment: false);

        // Assert
        settings.Should().NotBeNull();
        settings!.SigningKey.Should().Be(ValidKey);
        settings.JwksUri.Should().BeNull();
        settings.AllowedOrigins.Should().Equal("https://app.example.test");
        settings.AgentId.Should().Be("agent-1");
        settings.TrustForwardedHeaders.Should().BeFalse();
    }

    [Fact]
    public void FromConfiguration_WhenNoOriginIsConfigured_ShouldFail()
    {
        // Arrange & Act
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:AllowedOrigins:0", ""))), isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("*AllowedOrigins*");
    }

    [Fact]
    public void FromConfiguration_WhenBothJwksAndKeyAreSet_ShouldFail()
    {
        // Arrange & Act
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:Jwt:JwksUri", "https://issuer.test/jwks.json"))), isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_WhenNeitherJwksNorKeyIsSet_ShouldFail()
    {
        // Arrange & Act
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:Jwt:SigningKey", null))), isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_WhenSigningKeyIsTooShort_ShouldFail()
    {
        // Arrange & Act
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:Jwt:SigningKey", "short"))), isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("PublicChat:Jwt:Issuer")]
    [InlineData("PublicChat:Jwt:Audience")]
    public void FromConfiguration_WhenIssuerOrAudienceIsMissing_ShouldFail(string key)
    {
        // Arrange & Act
        var action = () => PublicChatSettings.FromConfiguration(Config(ValidHs256((key, null))), isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_WhenJwksUriIsHttpInProduction_ShouldFail()
    {
        // Arrange & Act
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(
                ("PublicChat:Jwt:SigningKey", null),
                ("PublicChat:Jwt:JwksUri", "http://issuer.test/jwks.json"))),
            isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_WhenJwksUriIsHttps_ShouldUseJwksAndNoKey()
    {
        // Arrange & Act
        var settings = PublicChatSettings.FromConfiguration(
            Config(ValidHs256(
                ("PublicChat:Jwt:SigningKey", null),
                ("PublicChat:Jwt:JwksUri", "https://issuer.test/jwks.json"))),
            isDevelopment: false);

        // Assert
        settings!.JwksUri.Should().Be("https://issuer.test/jwks.json");
        settings.SigningKey.Should().BeNull();
    }

    [Fact]
    public void FromConfiguration_WhenAgentIsMissingInProduction_ShouldFail()
    {
        // Arrange & Act
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:AgentId", null))), isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_WhenAgentIsMissingInDevelopment_ShouldFallBackToDefaultAgent()
    {
        // Arrange & Act
        var settings = PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:AgentId", null))), isDevelopment: true);

        // Assert
        settings!.AgentId.Should().BeNull();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.app.example.test")]
    [InlineData("app.example.test")]
    [InlineData("https://app.example.test/chat")]
    [InlineData("http://app.example.test")]
    public void FromConfiguration_WhenOriginIsUnsafeInProduction_ShouldFail(string origin)
    {
        // Arrange & Act
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:AllowedOrigins:0", origin))), isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_WhenOriginHasTrailingSlash_ShouldNormalizeIt()
    {
        // Arrange & Act
        var settings = PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:AllowedOrigins:0", "https://app.example.test/"))), isDevelopment: false);

        // Assert
        settings!.AllowedOrigins.Should().Equal("https://app.example.test");
    }

    [Fact]
    public void FromConfiguration_WhenLocalhostHttpOriginInDevelopment_ShouldAllowIt()
    {
        // Arrange & Act
        var settings = PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:AllowedOrigins:0", "http://localhost:3000"))), isDevelopment: true);

        // Assert
        settings!.AllowedOrigins.Should().Equal("http://localhost:3000");
    }

    [Fact]
    public void GetRateLimitKey_WhenAddressesShareAnIpv6Prefix_ShouldUseSamePartition()
    {
        // Arrange & Act
        var first = PublicChatExtensions.GetRateLimitKey(IPAddress.Parse("2001:db8:1:2:aaaa::1"));
        var second = PublicChatExtensions.GetRateLimitKey(IPAddress.Parse("2001:db8:1:2:bbbb::ffff"));
        var other = PublicChatExtensions.GetRateLimitKey(IPAddress.Parse("2001:db8:1:3::1"));

        // Assert
        first.Should().Be(second);
        other.Should().NotBe(first);
    }

    [Fact]
    public void GetRateLimitKey_WhenAddressIsIpv4MappedIpv6_ShouldUseDottedIpv4()
    {
        // Arrange & Act
        var key = PublicChatExtensions.GetRateLimitKey(IPAddress.Parse("::ffff:203.0.113.7"));

        // Assert
        key.Should().Be("203.0.113.7");
    }

    [Fact]
    public void GetRateLimitKey_WhenAddressIsMissing_ShouldNotThrow()
    {
        // Arrange, Act & Assert
        PublicChatExtensions.GetRateLimitKey(null).Should().NotBeNullOrEmpty();
    }
}
