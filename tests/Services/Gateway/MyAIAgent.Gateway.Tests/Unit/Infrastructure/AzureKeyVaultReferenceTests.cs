/**
 * MyAIAgent | Gateway · Tests
 *
 * Exercises the shared AAC loader resolving Key Vault references with its selected identity and preserving local overrides.
 */

using System.Net;
using System.Net.Http.Headers;
using Azure.Core;
using Azure.Core.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using MyAIAgent.Common.Infrastructure.Extensions;

namespace MyAIAgent.Gateway.Tests.Unit.Infrastructure;

public sealed class AzureKeyVaultReferenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_WhenAacContainsSecretReference_ShouldResolveWithSelectedIdentityAndPreserveOverrides(bool localOverride)
    {
        // Arrange
        var credential = new RecordingCredential();
        using var aacHttp = new HttpClient(new ConfigurationHandler());
        using var vaultHttp = new HttpClient(new VaultHandler());
        var builder = new ConfigurationBuilder().AddAzureAppConfiguration(options =>
        {
            AzureAppConfigurationExtension.ConfigureRemoteSettings(options,
                new Uri("https://configuration.azconfig.io"), credential, "McpServer:", "Development",
                vault => vault.ConfigureClientOptions(client =>
                {
                    client.Transport = new HttpClientTransport(vaultHttp);
                    client.Retry.MaxRetries = 0;
                }));
            options.ConfigureClientOptions(client =>
            {
                client.Transport = new HttpClientTransport(aacHttp);
                client.Retry.MaxRetries = 0;
            });
        });
        if (localOverride) builder.AddInMemoryCollection(new Dictionary<string, string?> { ["Tavily:ApiKey"] = "local-override" });

        // Act
        var configuration = builder.Build();

        // Assert
        configuration["Tavily:ApiKey"].Should().Be(localOverride ? "local-override" : "vault-test-value");
        configuration["McpServer:Tavily:ApiKey"].Should().BeNull();
        credential.Scopes.Should().Contain("https://vault.azure.net/.default");
    }

    [Fact]
    public void Build_WhenKeyVaultAccessIsDenied_ShouldFailInsteadOfUsingReferenceTextAsSecret()
    {
        // Arrange
        var credential = new RecordingCredential();
        using var aacHttp = new HttpClient(new ConfigurationHandler());
        using var vaultHttp = new HttpClient(new VaultHandler(deny: true));
        var builder = new ConfigurationBuilder().AddAzureAppConfiguration(options =>
        {
            AzureAppConfigurationExtension.ConfigureRemoteSettings(options,
                new Uri("https://configuration.azconfig.io"), credential, "McpServer:", "Development",
                vault => vault.ConfigureClientOptions(client =>
                {
                    client.Transport = new HttpClientTransport(vaultHttp);
                    client.Retry.MaxRetries = 0;
                }));
            options.ConfigureClientOptions(client =>
            {
                client.Transport = new HttpClientTransport(aacHttp);
                client.Retry.MaxRetries = 0;
            });
        });

        // Act
        var action = () => builder.Build();

        // Assert
        action.Should().Throw<KeyVaultReferenceException>();
        credential.Scopes.Should().Contain("https://vault.azure.net/.default");
    }

    private sealed class RecordingCredential : TokenCredential
    {
        public List<string> Scopes { get; } = [];
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Scopes.AddRange(requestContext.Scopes);
            return new AccessToken("test-access-token", DateTimeOffset.MaxValue);
        }
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class ConfigurationHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => Reply(request);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Reply(request));
        private static HttpResponseMessage Reply(HttpRequestMessage request)
        {
            const string secret = """
                {"key":"McpServer:Tavily:ApiKey","label":"Development","value":"{\"uri\":\"https://test-vault.vault.azure.net/secrets/tavily-api-key\"}","content_type":"application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8","etag":"test-etag","tags":{}}
                """;
            const string sentinel = """{"key":"McpServer:Sentinel","label":"Development","value":"1","etag":"sentinel-etag","tags":{}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.AbsolutePath.Contains("Sentinel")
                    ? sentinel : "{\"items\":[" + secret + "," + sentinel + "]}")
            };
        }
    }

    private sealed class VaultHandler(bool deny = false) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => Reply(request);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Reply(request));
        private HttpResponseMessage Reply(HttpRequestMessage request)
        {
            if (request.Headers.Authorization is null)
            {
                var challenge = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                challenge.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Bearer",
                    "authorization=\"https://login.microsoftonline.com/00000000-0000-0000-0000-000000000001\", resource=\"https://vault.azure.net\""));
                return challenge;
            }
            request.Headers.Authorization.Parameter.Should().Be("test-access-token");
            if (deny) return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{\"error\":{\"code\":\"Forbidden\",\"message\":\"Access denied\"}}") };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"value":"vault-test-value","id":"https://test-vault.vault.azure.net/secrets/tavily-api-key/test-version","attributes":{"enabled":true}}""")
            };
        }
    }
}
