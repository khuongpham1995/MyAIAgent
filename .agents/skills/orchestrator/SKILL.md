---
name: orchestrator
description: >
  Orchestrator service patterns for MyAIAgent. Covers the Microsoft Agents
  Framework (Microsoft.Agents.AI), IChatOrchestratorService abstraction,
  ChatOrchestratorService implementation, MCP client integration, and the
  POST /api/chat endpoint. Load when: working on the Orchestrator service,
  Microsoft Agents Framework, chat pipeline, AI provider configuration, or
  MCP tool dispatch from the Orchestrator.
---

# Orchestrator Service (MyAIAgent)

## Overview

`MyAIAgent.Orchestrator.*` is the chat and conversation service. It manages session history, calls `IChatClient` from Microsoft.Extensions.AI, and dispatches MCP tools. KernelMemory retrieval during chat is planned but not implemented. Installed Agents Framework packages do not imply that the current chat path constructs a framework agent.

## Project Structure

```
MyAIAgent.Orchestrator.Domain/
  Entities/ChatSession.cs                ← EF Core Cosmos entity (partition: UserId, so listing and lookups stay single-partition)
  Entities/ChatSessionMessage.cs         ← owned entity (Role, Content, CreatedAt)

MyAIAgent.Orchestrator.Application/
  Chat/Commands/Chat/                   ← ChatCommand, ChatHandler, ChatCommandValidator
  Common/Interfaces/IChatOrchestratorService.cs ← chat service port
  Common/Interfaces/IChatSessionRepository.cs   ← session persistence port
  Chat/ChatRequestDto.cs               ← HTTP input
  Common/Mappings/ChatMapping.cs         ← Mapster mapping for ChatRequestDto → ChatCommand
  IoCExtension.cs

MyAIAgent.Orchestrator.Infrastructure/
  Data/OrchestratorDbContext.cs          ← EF Core Cosmos (ChatSessions + AgentDefinitions containers; `myaiagent-test` locally and in staging, `myaiagent-prod` in production)
  Data/Repositories/ChatSessionRepository.cs ← IScoped
  Services/ChatOrchestratorService.cs   ← IScoped; uses IChatClient and retained MCP tools
  IoCExtension.cs            ← registers IChatClient, DB provider, HttpAuthEndpointFilter

MyAIAgent.Orchestrator.Presentation/
  Program.cs                             ← host configuration and endpoint composition
  Endpoints/                             ← chat, conversation, agent, health and service-auth composition modules
  appsettings.json
```

## Chat Flow

1. `POST /api/chat` receives `ChatRequestDto` with session, message, user identity, and optional agent selection
2. The injected Mapster mapper maps the request to `ChatCommand`
3. `ChatHandler` looks up the session by `SessionId` and `UserId` (or creates a new owned session), builds history, calls `IChatOrchestratorService.ChatAsync`
4. Appends user + assistant messages to session, persists via `IChatSessionRepository`
5. Returns `ResponseModel<ChatResponse(Reply, SessionId)>`

See `Application/Chat/Commands/Chat/ChatHandler.cs` for the current implementation.

## AI Provider

`ChatOrchestratorService` uses `IChatClient` from `Microsoft.Extensions.AI`. The provider (AzureOpenAI or OpenAI) is selected at startup via `AiProvider:Type` configuration.

`IoCExtension.AddInfrastructure` registers `IChatClient` as a singleton based on configuration. See `Infrastructure/IoCExtension.cs`.

## Streaming Chat

`POST /sse/chat` returns Server-Sent Events built from `StreamChatCommand : IStreamRequest<ChatStreamEvent>`
(`session`, `delta`, `title`, `done`, `error`). MediatR does not run `IPipelineBehavior` for stream requests,
so `StreamValidationBehavior` (Common.Application) validates them and is registered beside `ValidationBehavior`.
`ToServerSentEventsAsync` pulls the first event before returning so start-up failures such as validation still
produce a normal error response. `IChatOrchestratorService.StreamChatAsync` and `ChatAsync` share one turn setup
and release the MCP client when the turn ends. The `IChatClient` is wrapped with function invocation so tool calls run.

Sessions keep an LLM-generated title, a rolling `Summary` of older turns and `SummarizedMessageCount`;
`ChatSessionHistory` builds the history sent to the model and folds old turns once enough accumulate.
Conversation list/get/rename/delete handlers check that the session belongs to the requesting user id.

## Presentation Bootstrap

`Program.cs` wires: `AddAzureAppConfiguration` → `AddApplication` → `AddInfrastructure(isDevelopment)` → `AddGlobalExceptionHandling` → `AddSingleton(TimeProvider.System)` → `UseExceptionHandler` → endpoints via `MapGroup("").AddEndpointFilter(HttpAuthEndpointFilter)`.

`MapOrchestratorEndpoints()` composes the anonymous health module and the protected group.
Feature extensions use `IEndpointRouteBuilder Map[Module]Endpoints(this IEndpointRouteBuilder endpoints)`
and return the incoming builder. Put new routes in the appropriate module, retaining the protected parent group.

`TimeProvider` is injected for testable timestamps and current UTC context each chat turn — never use `DateTimeOffset.UtcNow` directly.

Agents expose only explicitly named, enabled tools. Empty/fully disabled lists grant none. `AgentHelper` wraps first-party `web_search` and `read_webpage` functions with a three-invocation budget per turn and source collection. Successful nonempty replies append a bounded Retrieved sources footer; streaming emits it as the final text delta before persistence. Error, cancellation, and empty output receive no footer. External tools with matching names do not establish sources.

## External Connections

**MCP Server** — `McpClient.CreateAsync` connects through a factory-managed `HttpClientTransport`, which auto-detects Streamable HTTP or legacy SSE. The local server exposes `/mcp` and retains the root endpoint for existing agent URLs. The MCP URL comes from the selected agent definition, not automatic Aspire service-discovery resolution.

**KernelMemory** — no retrieval client or call is currently registered in Orchestrator. Adding retrieval before model generation is future work; do not describe it as an existing request path.

## Configuration Shape (`appsettings.json`)

```json
{
  "AiProvider": { "Type": "AzureOpenAI" },
  "AzureOpenAI": { "Endpoint": "", "DeploymentName": "gpt-4o", "ApiKey": "" },
  "OpenAI": { "ApiKey": "", "ModelId": "gpt-4o" }
}
```

Use `dotnet user-secrets` for API keys. Never commit them.

`ServiceAuthorization` (shared key checked by `HttpAuthEndpointFilter`) is off in tracked local defaults, may be enabled locally, and must be on elsewhere:
the Orchestrator trusts the user id callers send, so `AddInfrastructure` throws outside Development unless
`ServiceAuthorization:Enabled` is true. The Gateway sends `Orchestrator:AccessKey` in the `Authorization` header.

## Key Packages (`Orchestrator.Infrastructure.csproj`)

| Package | Purpose |
|---|---|
| `Microsoft.Agents.AI` v1.25.0 | Microsoft Agents Framework |
| `Microsoft.Extensions.AI` | `IChatClient` abstraction |
| `Microsoft.Extensions.AI.OpenAI` | OpenAI `IChatClient` implementation |
| `Azure.AI.OpenAI` | Azure OpenAI client |

Cosmos databases and containers are provisioned by Bicep. Runtime uses `CosmosDb:Endpoint` and identity-based access; local developers sign in with Azure CLI and have only test-container permissions.
