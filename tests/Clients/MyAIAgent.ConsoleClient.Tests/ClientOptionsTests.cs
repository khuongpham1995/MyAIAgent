/**
 * MyAIAgent | Console Client · Tests
 *
 * Verifies that configured Gateway addresses permit local service roots and reject remote or ambiguous destinations.
 */

using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace MyAIAgent.ConsoleClient.Tests;

public sealed class ClientOptionsTests
{
    [Theory]
    [InlineData("http://localhost:5200")]
    [InlineData("https://127.0.0.1:7200/")]
    [InlineData("http://[::1]:5200/")]
    public void FromConfiguration_WhenLoopbackRoot_ShouldAcceptLocalAddress(string address)
    {
        // Arrange
        var configuration = CreateConfiguration(address);

        // Act
        var options = ClientOptions.FromConfiguration(configuration);

        // Assert
        options.Gateway.Should().Be(new Uri(address));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void FromConfiguration_WhenAddressIsMissing_ShouldRequireConfiguration(string? address)
    {
        // Arrange
        var configuration = CreateConfiguration(address);

        // Act
        Action act = () => ClientOptions.FromConfiguration(configuration);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Gateway:BaseUrl is required in configuration.");
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://192.168.1.2:5200")]
    [InlineData("https://localhost.evil.example")]
    [InlineData("https://user:password@localhost:7200")]
    [InlineData("https://localhost:7200/chat")]
    [InlineData("https://localhost:7200/?token=secret")]
    [InlineData("https://localhost:7200/#fragment")]
    [InlineData("file:///tmp/input")]
    public void FromConfiguration_WhenDestinationIsNotLocalRoot_ShouldRejectWithoutEchoingInput(string address)
    {
        // Arrange
        var configuration = CreateConfiguration(address);

        // Act
        Action act = () => ClientOptions.FromConfiguration(configuration);

        // Assert
        act.Should().Throw<ArgumentException>().Which.Message.Should().NotContain(address);
    }

    [Fact]
    public void FromConfiguration_WhenMcpEndpointConfigured_ShouldReadItsFullPath()
    {
        // Arrange
        var configuration = CreateConfiguration("https://localhost:7200", "https://mcp.example.com/custom/mcp");

        // Act
        var options = ClientOptions.FromConfiguration(configuration);

        // Assert
        options.McpServerEndpoint.Should().Be(new Uri("https://mcp.example.com/custom/mcp"));
    }

    [Fact]
    public void FromConfiguration_WhenMcpEndpointMissing_ShouldHaveNoHardcodedFallback()
    {
        // Arrange
        var configuration = CreateConfiguration("https://localhost:7200");

        // Act
        var options = ClientOptions.FromConfiguration(configuration);

        // Assert
        options.McpServerEndpoint.Should().BeNull();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("file:///tmp/mcp")]
    [InlineData("https://user:password@mcp.example.com/mcp")]
    [InlineData("https://mcp.example.com/mcp#fragment")]
    public void FromConfiguration_WhenMcpEndpointInvalid_ShouldRejectWithoutEchoingInput(string endpoint)
    {
        // Arrange
        var configuration = CreateConfiguration("https://localhost:7200", endpoint);

        // Act
        Action act = () => ClientOptions.FromConfiguration(configuration);

        // Assert
        act.Should().Throw<ArgumentException>().Which.Message.Should().NotContain(endpoint);
    }

    private static IConfiguration CreateConfiguration(string? address, string? mcpEndpoint = null) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gateway:BaseUrl"] = address,
            ["McpServer:Endpoint"] = mcpEndpoint,
        })
        .Build();
}
