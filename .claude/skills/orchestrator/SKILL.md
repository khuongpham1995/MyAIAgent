---
name: orchestrator
description: >
  Orchestrator service patterns for MyAIAgent. Covers the Microsoft Agents
  Framework (Microsoft.Agents.AI), IChatOrchestratorService abstraction,
  ChatOrchestratorService implementation, MCP client integration, and the
  POST /chat endpoint. Load when: working on the Orchestrator service,
  Microsoft Agents Framework, chat pipeline, AI provider configuration, or
  MCP tool dispatch from the Orchestrator.
---

# Orchestrator Service (MyAIAgent)

## Overview

The `MyAIAgent.Orchestrator.*` module is the **chat API** — it receives user messages, routes them through the Microsoft Agents Framework, and dispatches MCP tools as needed. It exposes `POST /chat`.

The Orchestrator follows Clean Architecture across four layers.

## Project Structure

```
MyAIAgent.Orchestrator.Domain/
  Interfaces/IChatOrchestratorService.cs  ← service contract
  Models/ChatMessage.cs                   ← role + content record
  Models/ChatRequest.cs                   ← userMessage + history
  Models/ChatResponse.cs                  ← reply string

MyAIAgent.Orchestrator.Application/
  Handlers/ChatHandler.cs                 ← ChatCommand + handler + validator
  Behaviors/ValidationBehavior.cs         ← MediatR pipeline behavior
  Extensions/IoCExtension.cs

MyAIAgent.Orchestrator.Infrastructure/
  Services/ChatOrchestratorService.cs     ← implements IChatOrchestratorService
  Extensions/IoCExtension.cs             ← registers IChatClient, Scrutor scan

MyAIAgent.Orchestrator.Presentation/
  Program.cs                              ← ASP.NET Core host, POST /chat
  appsettings.json                        ← AI provider config
```

## Domain Interface

```csharp
// Orchestrator.Domain/Interfaces/IChatOrchestratorService.cs
public interface IChatOrchestratorService
{
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct);
}
```

```csharp
// Orchestrator.Domain/Models/
public sealed record ChatMessage(string Role, string Content);
public sealed record ChatRequest(string UserMessage, IReadOnlyList<ChatMessage> History);
public sealed record ChatResponse(string Reply);
```

## Application Handler

```csharp
public sealed record ChatCommand(string UserMessage, IReadOnlyList<ChatMessage> History)
    : IRequest<ResponseModel<ChatResponse>>;

public sealed class ChatHandler(IChatOrchestratorService orchestratorService)
    : IRequestHandler<ChatCommand, ResponseModel<ChatResponse>>
{
    public async Task<ResponseModel<ChatResponse>> Handle(
        ChatCommand command, CancellationToken ct)
    {
        var request = new ChatRequest(command.UserMessage, command.History);
        var response = await orchestratorService.ChatAsync(request, ct);
        return ResponseModel<ChatResponse>.Succeed(response);
    }
}

public sealed class ChatCommandValidator : AbstractValidator<ChatCommand>
{
    public ChatCommandValidator()
    {
        RuleFor(x => x.UserMessage).NotEmpty().MaximumLength(4000);
    }
}
```

## Infrastructure — Microsoft Agents Framework

`ChatOrchestratorService` uses `IChatClient` from `Microsoft.Extensions.AI` (the foundation that `Microsoft.Agents.AI` builds on):

```csharp
public sealed class ChatOrchestratorService(IChatClient chatClient)
    : IChatOrchestratorService, IScoped
{
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct)
    {
        var messages = request.History
            .Select(m => new Microsoft.Extensions.AI.ChatMessage(
                m.Role == "assistant" ? ChatRole.Assistant : ChatRole.User,
                m.Content))
            .ToList();

        messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, request.UserMessage));

        var completion = await chatClient.CompleteAsync(messages, cancellationToken: ct);
        return new ChatResponse(completion.Message.Text ?? string.Empty);
    }
}
```

## Infrastructure — DI Registration

`IoCExtension` registers the `IChatClient` based on the configured AI provider:

```csharp
public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
{
    services
        .AddTransientAsMatchingInterfaces(typeof(IoCExtension).Assembly)
        .AddScopedAsMatchingInterfaces(typeof(IoCExtension).Assembly)
        .AddSingletonAsMatchingInterfaces(typeof(IoCExtension).Assembly);

    var providerType = configuration["AiProvider:Type"]
        ?? throw new InvalidOperationException("AiProvider:Type is required");

    if (providerType == "AzureOpenAI")
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"]!;
        var deployment = configuration["AzureOpenAI:DeploymentName"]!;
        var apiKey = configuration["AzureOpenAI:ApiKey"]!;

        services.AddSingleton<IChatClient>(
            new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
                .AsChatClient(deployment));
    }
    else
    {
        var apiKey = configuration["OpenAI:ApiKey"]!;
        var modelId = configuration["OpenAI:ModelId"]!;

        services.AddSingleton<IChatClient>(
            new OpenAIClient(apiKey).AsChatClient(modelId));
    }
}
```

## Presentation — POST /chat

```csharp
// Orchestrator.Presentation/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

var app = builder.Build();

app.MapPost("/chat", async (ChatCommand command, ISender sender, CancellationToken ct) =>
{
    var result = await sender.Send(command, ct);
    return result.IsSuccess
        ? Results.Ok(result.Data)
        : Results.BadRequest(result.Errors);
});

await app.RunAsync();
```

## Configuration (`appsettings.json`)

```json
{
  "AiProvider": {
    "Type": "AzureOpenAI"
  },
  "AzureOpenAI": {
    "Endpoint": "",
    "DeploymentName": "gpt-4o",
    "ApiKey": ""
  },
  "OpenAI": {
    "ApiKey": "",
    "ModelId": "gpt-4o"
  }
}
```

Use `dotnet user-secrets` for API keys. Never commit them.

## Packages (`MyAIAgent.Orchestrator.Infrastructure.csproj`)

| Package | Purpose |
|---|---|
| `Microsoft.Agents.AI` v1.0.0-rc1 | Microsoft Agents Framework |
| `Microsoft.Agents.AI.OpenAI` v1.0.0-rc1 | OpenAI provider for Agents |
| `Microsoft.Extensions.AI` | IChatClient abstraction |
| `Microsoft.Extensions.AI.OpenAI` | OpenAI IChatClient implementation |
| `Azure.AI.OpenAI` | Azure OpenAI client |
| `Azure.Identity` | Azure credential types |

## MCP Tools — Connecting to the MCP Server

The Orchestrator can register MCP tools from the MCP Server via `McpClient`. This enables the agent to invoke tools during a conversation:

```csharp
// In Infrastructure DI extension — fetch available MCP tools
var mcpEndpoint = configuration["McpServer:BaseUrl"]!;
var mcpClient = await McpClientFactory.CreateAsync(
    new SseClientTransport(new SseClientTransportOptions { Endpoint = new Uri(mcpEndpoint) }),
    cancellationToken: ct);
var tools = await mcpClient.ListToolsAsync(cancellationToken: ct);
// Register tools with the agent...
```

The MCP Server URL is injected by Aspire via service discovery.

## Anti-patterns

```csharp
// DON'T — call IChatClient directly from Presentation (bypass MediatR)
app.MapPost("/chat", async (IChatClient client) => ...);

// DON'T — hardcode AI provider in code
var client = new OpenAIClient("sk-hardcoded");  // use configuration + user-secrets

// DON'T — drop history (stateless handlers lose conversation context)
var messages = new List<ChatMessage> { new(ChatRole.User, request.UserMessage) };
// Missing: prepend history from request.History

// DON'T — drop CancellationToken
var completion = await chatClient.CompleteAsync(messages);  // should pass ct
```
