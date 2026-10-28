/**
 * MyAIAgent | Console Client · Tests
 *
 * Exercises JSON, environment, and command-line precedence with isolated settings files and restored process state.
 */

using System.Text.Json;
using FluentAssertions;

namespace MyAIAgent.ConsoleClient.Tests;

[Collection("Configuration")]
public sealed class ConsoleConfigurationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"myaiagent-config-{Guid.NewGuid():N}");
    private readonly Dictionary<string, string?> previousEnvironment = new[]
        { "DOTNET_ENVIRONMENT", "Gateway__BaseUrl", "Gateway:BaseUrl", "McpServer__Endpoint", "McpServer:Endpoint" }
        .ToDictionary(key => key, Environment.GetEnvironmentVariable);

    public ConsoleConfigurationTests()
    {
        Directory.CreateDirectory(directory);
        foreach (var key in previousEnvironment.Keys)
            Environment.SetEnvironmentVariable(key, null);
        WriteSettings("appsettings.json", "https://localhost:7300");
    }

    [Fact]
    public void Load_WhenNoOverrides_ShouldReadJsonFromSuppliedDirectory()
    {
        // Arrange / Act
        var options = ConsoleConfiguration.Load([], directory);

        // Assert
        options.Gateway.Should().Be(new Uri("https://localhost:7300"));
    }

    [Fact]
    public void Load_WhenEnvironmentSelected_ShouldOverrideBaseJson()
    {
        // Arrange
        WriteSettings("appsettings.Development.json", "http://localhost:5300");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

        // Act
        var options = ConsoleConfiguration.Load([], directory);

        // Assert
        options.Gateway.Should().Be(new Uri("http://localhost:5300"));
    }

    [Fact]
    public void Load_WhenEnvironmentFileMissing_ShouldUseBaseJson()
    {
        // Arrange
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

        // Act
        var options = ConsoleConfiguration.Load([], directory);

        // Assert
        options.Gateway.Should().Be(new Uri("https://localhost:7300"));
    }

    [Fact]
    public void Load_WhenEnvironmentVariableSet_ShouldOverrideBothJsonFiles()
    {
        // Arrange
        WriteSettings("appsettings.Development.json", "http://localhost:5300");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("Gateway__BaseUrl", "https://localhost:7400");

        // Act
        var options = ConsoleConfiguration.Load([], directory);

        // Assert
        options.Gateway.Should().Be(new Uri("https://localhost:7400"));
    }

    [Fact]
    public void Load_WhenMcpEnvironmentVariableSet_ShouldOverrideJsonEndpoint()
    {
        // Arrange
        WriteSettings("appsettings.json", "https://localhost:7300", "http://localhost:5300/mcp");
        Environment.SetEnvironmentVariable("McpServer__Endpoint", "http://localhost:5400/custom-mcp");

        // Act
        var options = ConsoleConfiguration.Load([], directory);

        // Assert
        options.McpServerEndpoint.Should().Be(new Uri("http://localhost:5400/custom-mcp"));
    }

    [Fact]
    public void Load_WhenGatewayArgumentPresent_ShouldOverrideEnvironmentAndJson()
    {
        // Arrange
        WriteSettings("appsettings.Development.json", "http://localhost:5300");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("Gateway__BaseUrl", "https://localhost:7400");

        // Act
        var options = ConsoleConfiguration.Load(["--gateway", "http://localhost:5400"], directory);

        // Assert
        options.Gateway.Should().Be(new Uri("http://localhost:5400"));
    }

    [Theory]
    [InlineData("json")]
    [InlineData("environment")]
    [InlineData("argument")]
    public void Load_WhenEffectiveAddressIsRemote_ShouldRejectEverySource(string source)
    {
        // Arrange
        var address = "https://example.com";
        if (source == "json") WriteSettings("appsettings.json", address);
        if (source == "environment") Environment.SetEnvironmentVariable("Gateway__BaseUrl", address);
        string[] args = source == "argument" ? ["--gateway", address] : [];

        // Act
        Action act = () => ConsoleConfiguration.Load(args, directory);

        // Assert
        act.Should().Throw<ArgumentException>().Which.Message.Should().NotContain(address);
    }

    [Fact]
    public void Load_WhenJsonMalformed_ShouldFailInsteadOfUsingFallback()
    {
        // Arrange
        File.WriteAllText(Path.Combine(directory, "appsettings.json"), "{ invalid");

        // Act
        Action act = () => ConsoleConfiguration.Load([], directory);

        // Assert
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_WhenRequiredFileMissing_ShouldFailInsteadOfUsingFallback()
    {
        // Arrange
        File.Delete(Path.Combine(directory, "appsettings.json"));

        // Act
        Action act = () => ConsoleConfiguration.Load([], directory);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Load_WhenArgumentsUnknownOrIncomplete_ShouldReject()
    {
        // Arrange
        string[][] arguments = [["--gateway"], ["--token", "secret"], ["--gateway", "http://localhost", "extra"]];

        // Act / Assert
        foreach (var args in arguments)
            FluentActions.Invoking(() => ConsoleConfiguration.Load(args, directory)).Should().Throw<ArgumentException>();
    }

    public void Dispose()
    {
        foreach (var (key, value) in previousEnvironment)
            Environment.SetEnvironmentVariable(key, value);
        Directory.Delete(directory, recursive: true);
    }

    private void WriteSettings(string name, string address, string? mcpEndpoint = null) => File.WriteAllText(Path.Combine(directory, name),
        JsonSerializer.Serialize(new { Gateway = new { BaseUrl = address }, McpServer = new { Endpoint = mcpEndpoint } }));
}
