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

`MyAIAgent.Orchestrator.*` is the **chat API and RAG consumer** — receives user messages, retrieves relevant context from KernelMemory, manages session history, routes through the Microsoft Agents Framework, dispatches MCP tools, and exposes `POST /chat`.

## Project Structure

```
MyAIAgent.Orchestrator.Domain/
  Entities/ChatSession.cs                ← EF Core Cosmos entity (partition: UserId, so listing and lookups stay single-partition)
  Entities/ChatSessionMessage.cs         ← owned entity (Role, Content, CreatedAt)
  Models/ChatMessage.cs                  ← role + content (for AI calls)
  Models/ChatRequest.cs                  ← UserMessage + History
  Models/ChatResponse.cs                 ← (Reply, SessionId)

MyAIAgent.Orchestrator.Application/
  Chat/Commands/Chat/                   ← ChatCommand, ChatHandler, ChatCommandValidator
  Interfaces/IChatOrchestratorService.cs ← chat service port
  Interfaces/IChatSessionRepository.cs   ← session persistence port
  Models/ChatRequestDto.cs               ← HTTP input
  Common/Mappings/ChatMapping.cs         ← Mapster mapping for ChatRequestDto → ChatCommand
  Extensions/IoCExtension.cs

MyAIAgent.Orchestrator.Infrastructure/
  Data/OrchestratorDbContext.cs          ← EF Core Cosmos (ChatSessions + AgentDefinitions containers; `myaiagent-test` locally and in staging, `myaiagent-prod` in production)
  Repositories/ChatSessionRepository.cs ← IScoped
  Services/ChatOrchestratorService.cs   ← IScoped; wraps IChatClient + IKernelMemoryClient (planned)
  Extensions/IoCExtension.cs            ← registers IChatClient, DB provider, HttpAuthEndpointFilter

MyAIAgent.Orchestrator.Presentation/
  Program.cs                             ← POST /chat, Aspire + Cosmos wiring
  appsettings.json
```

## Chat Flow

1. `POST /chat` receives `ChatRequestDto(SessionId?, UserMessage)`
2. The injected Mapster mapper maps the request to `ChatCommand`
3. `ChatHandler` looks up session by `SessionId` (or creates new), builds history, calls `IChatOrchestratorService.ChatAsync`
4. Appends user + assistant messages to session, persists via `IChatSessionRepository`
5. Returns `ResponseModel<ChatResponse(Reply, SessionId)>`

See `Application/Chat/Commands/Chat/ChatHandler.cs` for the current implementation.

## AI Provider

`ChatOrchestratorService` uses `IChatClient` from `Microsoft.Extensions.AI`. The provider (AzureOpenAI or OpenAI) is selected at startup via `AiProvider:Type` configuration.

`IoCExtension.AddInfrastructure` registers `IChatClient` as a singleton based on configuration. See `Infrastructure/IoCExtension.cs`.

## Streaming Chat

`POST /chat/stream` returns Server-Sent Events built from `StreamChatCommand : IStreamRequest<ChatStreamEvent>`
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

`TimeProvider` is injected for testable timestamps — never use `DateTimeOffset.UtcNow` directly.

## External Connections

**MCP Server** — tools are registered via `McpClientFactory` + `SseClientTransport`. The MCP Server URL is injected by Aspire via service discovery.

**KernelMemory** — the Orchestrator calls KernelMemory's `/ask` endpoint before generating a reply, prepending the retrieved context to the user prompt. Implemented via a typed `HttpClient` (`IKernelMemoryClient`) registered in Infrastructure. This connection is **planned but not yet implemented**.

## Configuration Shape (`appsettings.json`)

```json
{
  "AiProvider": { "Type": "AzureOpenAI" },
  "AzureOpenAI": { "Endpoint": "", "DeploymentName": "gpt-4o", "ApiKey": "" },
  "OpenAI": { "ApiKey": "", "ModelId": "gpt-4o" }
}
```

Use `dotnet user-secrets` for API keys. Never commit them.

`ServiceAuthorization` (shared key checked by `HttpAuthEndpointFilter`) is off in Development and must be on elsewhere:
the Orchestrator trusts the user id callers send, so `AddInfrastructure` throws outside Development unless
`ServiceAuthorization:Enabled` is true. The Gateway sends `Orchestrator:AccessKey` in the `Authorization` header.

## Key Packages (`Orchestrator.Infrastructure.csproj`)

| Package | Purpose |
|---|---|
| `Microsoft.Agents.AI` v1.0.0-rc1 | Microsoft Agents Framework |
| `Microsoft.Extensions.AI` | `IChatClient` abstraction |
| `Microsoft.Extensions.AI.OpenAI` | OpenAI `IChatClient` implementation |
| `Azure.AI.OpenAI` | Azure OpenAI client |

Cosmos databases and containers are provisioned by Bicep. Runtime uses `CosmosDb:Endpoint` and identity-based access; local developers sign in with Azure CLI and have only test-container permissions.
