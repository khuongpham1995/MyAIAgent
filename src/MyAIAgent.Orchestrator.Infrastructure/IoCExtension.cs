using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Application.Extensions;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Common.Infrastructure.Filters;
using MyAIAgent.Orchestrator.Infrastructure.Data;
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

        if (isDevelopment)
            services.AddSqliteDb<OrchestratorDbContext>("orchestrator.db");
        else
            services.AddCosmosDb<OrchestratorDbContext>(configuration);

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

        services.AddSingleton(chatClient);
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
