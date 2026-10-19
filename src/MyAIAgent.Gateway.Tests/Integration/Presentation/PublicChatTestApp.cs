using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.Text;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Gateway.Application;
using MyAIAgent.Gateway.Application.Common.Interfaces;
using MyAIAgent.Gateway.Presentation.PublicApi;

namespace MyAIAgent.Gateway.Tests.Integration.Presentation;

internal sealed class PublicChatTestApp : IAsyncDisposable
{
    public const string Issuer = "https://issuer.test";
    public const string Audience = "chat-api";
    // 64 bytes so the same secret can also sign HS384 tokens in the algorithm-allowlist test.
    public static readonly string SigningKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
    public const string AllowedOrigin = "https://app.example.test";
    public const string ConfiguredAgent = "configured-agent";

    private readonly WebApplication app;

    public HttpClient Client { get; }
    public Mock<IOrchestratorStreamClient> StreamClient { get; }
    public Mock<IOrchestratorClient> OrchestratorClient { get; }

    private PublicChatTestApp(
        WebApplication app,
        HttpClient client,
        Mock<IOrchestratorStreamClient> streamClient,
        Mock<IOrchestratorClient> orchestratorClient)
    {
        this.app = app;
        Client = client;
        StreamClient = streamClient;
        OrchestratorClient = orchestratorClient;
    }

    public static async Task<PublicChatTestApp> StartAsync(PublicChatRateLimits? limits = null)
    {
        var settings = new PublicChatSettings(
            Issuer,
            Audience,
            JwksUri: null,
            SigningKey,
            [AllowedOrigin],
            ConfiguredAgent,
            TrustForwardedHeaders: false,
            limits ?? new PublicChatRateLimits());

        var streamClient = new Mock<IOrchestratorStreamClient>();
        var orchestratorClient = new Mock<IOrchestratorClient>();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApplication();
        builder.Services.AddGlobalExceptionHandling();
        builder.Services.AddSingleton(streamClient.Object);
        builder.Services.AddSingleton(orchestratorClient.Object);
        // Unrelated handlers are registered by AddApplication and validated at startup in Development.
        builder.Services.AddSingleton(new Mock<IMcpServerClient>().Object);
        builder.Services.AddSingleton(new Mock<IKernelMemoryClient>().Object);
        builder.Services.AddPublicChat(settings, isDevelopment: true);

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UsePublicChat(settings);
        app.UseAuthorization();
        app.MapPublicChat(settings);
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        var client = new HttpClient { BaseAddress = new Uri(address) };

        return new PublicChatTestApp(app, client, streamClient, orchestratorClient);
    }

    public static string CreateToken(
        string? subject = "visitor-a",
        string issuer = Issuer,
        string audience = Audience,
        string? signingKey = null,
        DateTime? expires = null,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var claims = new Dictionary<string, object>();
        if (subject is not null)
            claims["sub"] = subject;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            NotBefore = DateTime.UtcNow.AddMinutes(-10),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? SigningKey)),
                algorithm),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public static HttpRequestMessage ChatRequest(string? token, string body = "{\"userMessage\":\"Hi\"}")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/public/chat/stream")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (token is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.StopAsync();
        await app.DisposeAsync();
    }
}
