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

`MyAIAgent.Orchestrator.*` is the **chat API** — receives user messages, manages session history, routes through the Microsoft Agents Framework, and exposes `POST /chat`.

## Project Structure

```
MyAIAgent.Orchestrator.Domain/
  Interfaces/IChatOrchestratorService.cs  ← Task<ResponseModel<string>> ChatAsync(...)
  Interfaces/IChatSessionRepository.cs   ← find/add/update ChatSession
  Entities/ChatSession.cs                ← EF Core Cosmos entity (partition: SessionId)
  Entities/ChatSessionMessage.cs         ← owned entity (Role, Content, CreatedAt)
  Models/ChatMessage.cs                  ← role + content (for AI calls)
  Models/ChatRequest.cs                  ← UserMessage + History
  Models/ChatRequestDto.cs               ← (SessionId?, UserMessage) — HTTP input
  Models/ChatResponse.cs                 ← (Reply, SessionId)

MyAIAgent.Orchestrator.Application/
  Handlers/ChatHandler.cs                ← ChatCommand + handler + validator
  Mappers/ChatCommandMapper.cs           ← ChatRequestDto.ToCommand()
  Extensions/IoCExtension.cs

MyAIAgent.Orchestrator.Infrastructure/
  Data/OrchestratorDbContext.cs          ← EF Core Cosmos (ChatSessions container)
  Repositories/ChatSessionRepository.cs ← IScoped
  Services/ChatOrchestratorService.cs   ← IScoped; wraps IChatClient
  Extensions/IoCExtension.cs            ← registers IChatClient, AddCosmosDb<OrchestratorDbContext>

MyAIAgent.Orchestrator.Presentation/
  Program.cs                             ← POST /chat, Aspire + Cosmos wiring
  appsettings.json
```

## Chat Flow

1. `POST /chat` receives `ChatRequestDto(SessionId?, UserMessage)`
2. `request.ToCommand()` maps to `ChatCommand`
3. `ChatHandler` looks up session by `SessionId` (or creates new), builds history, calls `IChatOrchestratorService.ChatAsync`
4. Appends user + assistant messages to session, persists via `IChatSessionRepository`
5. Returns `ResponseModel<ChatResponse(Reply, SessionId)>`

See `Application/Handlers/ChatHandler.cs` for the current implementation.

## AI Provider

`ChatOrchestratorService` uses `IChatClient` from `Microsoft.Extensions.AI`. The provider (AzureOpenAI or OpenAI) is selected at startup via `AiProvider:Type` configuration.

`IoCExtension.AddInfrastructure` registers `IChatClient` as a singleton based on configuration. See `Infrastructure/Extensions/IoCExtension.cs`.

## Presentation Bootstrap

`Program.cs` wires: `AddAzureAppConfiguration` → `AddApplication` → `AddInfrastructure` → `AddGlobalExceptionHandling` → `AddSingleton(TimeProvider.System)` → `UseExceptionHandler` → `EnsureCosmosDbCreatedAsync` → `MapPost("/chat", ...)`.

`TimeProvider` is injected for testable timestamps — never use `DateTimeOffset.UtcNow` directly.

## MCP Tools Connection

The Orchestrator can register MCP tools from the MCP Server via `McpClientFactory` + `SseClientTransport`. The MCP Server URL is injected by Aspire via service discovery.

## Configuration Shape (`appsettings.json`)

```json
{
  "AiProvider": { "Type": "AzureOpenAI" },
  "AzureOpenAI": { "Endpoint": "", "DeploymentName": "gpt-4o", "ApiKey": "" },
  "OpenAI": { "ApiKey": "", "ModelId": "gpt-4o" }
}
```

Use `dotnet user-secrets` for API keys. Never commit them.

## Key Packages (`Orchestrator.Infrastructure.csproj`)

| Package | Purpose |
|---|---|
| `Microsoft.Agents.AI` v1.0.0-rc1 | Microsoft Agents Framework |
| `Microsoft.Extensions.AI` | `IChatClient` abstraction |
| `Microsoft.Extensions.AI.OpenAI` | OpenAI `IChatClient` implementation |
| `Azure.AI.OpenAI` | Azure OpenAI client |
