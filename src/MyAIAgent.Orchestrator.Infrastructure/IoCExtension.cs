/**
 * MyAIAgent | Orchestrator · Infrastructure
 *
 * Registers Cosmos persistence, the function-invoking chat client, MCP transport, and service authorization.
 */

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Application.Extensions;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Common.Infrastructure.Filters;
using MyAIAgent.Common.Infrastructure.Http;
using MyAIAgent.Orchestrator.Infrastructure.Data;
using MyAIAgent.Orchestrator.Infrastructure.Helpers;
using OpenAI;

namespace MyAIAgent.Orchestrator.Infrastructure;

public static class IoCExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        services
            .AddTransientAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddScopedAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddSingletonAsMatchingInterfaces(typeof(IoCExtension).Assembly);

        services.AddCosmosDb<OrchestratorDbContext>(configuration, isDevelopment);

        // Callers supply the user id, so outside Development only holders of the shared service key may call this API.
        if (!isDevelopment && !configuration.GetValue<bool>("ServiceAuthorization:Enabled"))
            throw new InvalidOperationException("ServiceAuthorization:Enabled must be true outside Development.");

        var mcpSettings = McpServiceClientSettings.Load(configuration, isDevelopment);
        // Tool invocations may have side effects, so the MCP transport deliberately has no retry policy.
        services.AddHttpClient<IAgentHelper, AgentHelper>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.BaseAddress = mcpSettings.Origin;
        }).AddMcpServiceAuthentication(mcpSettings);

        services.Configure<ServiceAuthorizationModel>(
            configuration.GetSection("ServiceAuthorization"));

        services.AddSingleton<HttpAuthEndpointFilter>();

        var providerType = configuration["AiProvider:Type"]
            ?? throw new InvalidOperationException("Configuration key 'AiProvider:Type' is missing");

        var chatClient = providerType switch
        {
            "AzureOpenAI" => BuildAzureOpenAiClient(configuration),
            "OpenAI" => BuildOpenAiClient(configuration),
            _ => throw new InvalidOperationException($"Unsupported AI provider: '{providerType}'. Supported values: AzureOpenAI, OpenAI")
        };

        // Function invocation executes the agent's MCP tool calls; without it tool requests never get an answer.
        services.AddSingleton(new ChatClientBuilder(chatClient).UseFunctionInvocation().Build());
    }

    private static IChatClient BuildAzureOpenAiClient(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("Configuration key 'AzureOpenAI:Endpoint' is missing");
        var deployment = configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("Configuration key 'AzureOpenAI:DeploymentName' is missing");
        var apiKey = configuration["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("Configuration key 'AzureOpenAI:ApiKey' is missing");

        return new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
            .GetChatClient(deployment)
            .AsIChatClient();
    }

    private static IChatClient BuildOpenAiClient(IConfiguration configuration)
    {
        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("Configuration key 'OpenAI:ApiKey' is missing");
        var modelId = configuration["OpenAI:ModelId"]
            ?? throw new InvalidOperationException("Configuration key 'OpenAI:ModelId' is missing");

        return new OpenAIClient(apiKey)
            .GetChatClient(modelId)
            .AsIChatClient();
    }
}
