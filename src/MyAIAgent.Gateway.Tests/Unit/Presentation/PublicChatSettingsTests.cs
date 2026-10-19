using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MyAIAgent.Gateway.Presentation.PublicApi;

namespace MyAIAgent.Gateway.Tests.Unit.Presentation;

public sealed class PublicChatSettingsTests
{
    private const string ValidKey = "0123456789abcdef0123456789abcdef";

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
        var settings = PublicChatSettings.FromConfiguration(Config(), isDevelopment: false);

        settings.Should().BeNull();
    }

    [Fact]
    public void FromConfiguration_WhenOnlyEmptyPlaceholdersArePresent_ShouldDisablePublicChat()
    {
        var settings = PublicChatSettings.FromConfiguration(
            Config(("PublicChat:Jwt:Issuer", ""), ("PublicChat:Jwt:SigningKey", "")), isDevelopment: false);

        settings.Should().BeNull();
    }

    [Fact]
    public void FromConfiguration_WhenHs256ConfigurationIsComplete_ShouldReturnSettings()
    {
        var settings = PublicChatSettings.FromConfiguration(Config(ValidHs256()), isDevelopment: false);

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
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:AllowedOrigins:0", ""))), isDevelopment: false);

        action.Should().Throw<InvalidOperationException>().WithMessage("*AllowedOrigins*");
    }

    [Fact]
    public void FromConfiguration_WhenBothJwksAndKeyAreSet_ShouldFail()
    {
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:Jwt:JwksUri", "https://issuer.test/jwks.json"))), isDevelopment: false);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_WhenNeitherJwksNorKeyIsSet_ShouldFail()
    {
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:Jwt:SigningKey", null))), isDevelopment: false);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_WhenSigningKeyIsTooShort_ShouldFail()
    {
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:Jwt:SigningKey", "short"))), isDevelopment: false);

        action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("PublicChat:Jwt:Issuer")]
    [InlineData("PublicChat:Jwt:Audience")]
    public void FromConfiguration_WhenIssuerOrAudienceIsMissing_ShouldFail(string key)
    {
        var action = () => PublicChatSettings.FromConfiguration(Config(ValidHs256((key, null))), isDevelopment: false);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_WhenJwksUriIsHttpInProduction_ShouldFail()
    {
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(
                ("PublicChat:Jwt:SigningKey", null),
                ("PublicChat:Jwt:JwksUri", "http://issuer.test/jwks.json"))),
            isDevelopment: false);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_WhenJwksUriIsHttps_ShouldUseJwksAndNoKey()
    {
        var settings = PublicChatSettings.FromConfiguration(
            Config(ValidHs256(
                ("PublicChat:Jwt:SigningKey", null),
                ("PublicChat:Jwt:JwksUri", "https://issuer.test/jwks.json"))),
            isDevelopment: false);

        settings!.JwksUri.Should().Be("https://issuer.test/jwks.json");
        settings.SigningKey.Should().BeNull();
    }

    [Fact]
    public void FromConfiguration_WhenAgentIsMissingInProduction_ShouldFail()
    {
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:AgentId", null))), isDevelopment: false);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_WhenAgentIsMissingInDevelopment_ShouldFallBackToDefaultAgent()
    {
        var settings = PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:AgentId", null))), isDevelopment: true);

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
        var action = () => PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:AllowedOrigins:0", origin))), isDevelopment: false);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_WhenOriginHasTrailingSlash_ShouldNormalizeIt()
    {
        var settings = PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:AllowedOrigins:0", "https://app.example.test/"))), isDevelopment: false);

        settings!.AllowedOrigins.Should().Equal("https://app.example.test");
    }

    [Fact]
    public void FromConfiguration_WhenLocalhostHttpOriginInDevelopment_ShouldAllowIt()
    {
        var settings = PublicChatSettings.FromConfiguration(
            Config(ValidHs256(("PublicChat:AllowedOrigins:0", "http://localhost:3000"))), isDevelopment: true);

        settings!.AllowedOrigins.Should().Equal("http://localhost:3000");
    }

    [Fact]
    public void GetRateLimitKey_WhenAddressesShareAnIpv6Prefix_ShouldUseSamePartition()
    {
        var first = PublicChatExtensions.GetRateLimitKey(IPAddress.Parse("2001:db8:1:2:aaaa::1"));
        var second = PublicChatExtensions.GetRateLimitKey(IPAddress.Parse("2001:db8:1:2:bbbb::ffff"));
        var other = PublicChatExtensions.GetRateLimitKey(IPAddress.Parse("2001:db8:1:3::1"));

        first.Should().Be(second);
        other.Should().NotBe(first);
    }

    [Fact]
    public void GetRateLimitKey_WhenAddressIsIpv4MappedIpv6_ShouldUseDottedIpv4()
    {
        var key = PublicChatExtensions.GetRateLimitKey(IPAddress.Parse("::ffff:203.0.113.7"));

        key.Should().Be("203.0.113.7");
    }

    [Fact]
    public void GetRateLimitKey_WhenAddressIsMissing_ShouldNotThrow()
    {
        PublicChatExtensions.GetRateLimitKey(null).Should().NotBeNullOrEmpty();
    }
}
