# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Project Instructions

Before responding, read the following instruction files and follow their rules throughout the session:

- `.claude/instructions/follow-up-questions.md` — confidence and clarification rules

# MyAIAgent

An AI agent backend with three services: **Orchestrator** (Microsoft Agents Framework), **KernelMemory** (RAG), and **MCP Server** (tools). All services follow Clean Architecture.

## Tech Stack

- **.NET 10** / C# 14
- **.NET Aspire** — multi-service orchestration (`MyAIAgent.All`)
- **Microsoft Agents Framework** (`Microsoft.Agents.AI` v1.0.0-rc1) — Orchestrator AI agent
- **Microsoft.Extensions.AI** + **Microsoft.Extensions.AI.OpenAI** — AI client abstractions
- **ASP.NET Core** — HTTP hosts for Orchestrator (`POST /chat`) and MCP Server (SSE + POST)
- **Microsoft KernelMemory** (`Microsoft.KernelMemory.Service.AspNetCore`) — self-hosted RAG daemon
- **ModelContextProtocol SDK** (v0.5.0-preview.1) — MCP tool registration and transport
- **MediatR** (v12.5.0) — CQRS command/query dispatch in all Application layers
- **FluentValidation** (v11.11.0) — Request validation via pipeline behaviors
- **Polly** (via `Microsoft.Extensions.Http.Resilience`) — HTTP retry with exponential backoff
- **Scrutor** (v7.0.0) — Convention-based DI scanning via marker interfaces
- **xUnit** + **Moq** + **FluentAssertions** — Testing

## Architecture

Three services, each with Clean Architecture. One shared Common base.

```
src/
  MyAIAgent.All/                               # .NET Aspire orchestration
  Common/
    MyAIAgent.Common.Domain/                   # ResponseModel<T>, ITransient/IScoped/ISingleton
    MyAIAgent.Common.Application/              # ValidationBehavior<TRequest,TResponse> (always throws ValidationException)
    MyAIAgent.Common.Infrastructure/           # ServiceCollectionExtension (Scrutor), RetryExtension (Polly),
                                               # GlobalExceptionHandler, ExceptionHandlingExtension,
                                               # CosmosDbExtension, AzureAppConfigurationExtension
  McpServer/
    MyAIAgent.McpServer.Domain/                # Entities, interfaces, MCP constants
    MyAIAgent.McpServer.Application/           # MediatR handlers, validators, pipeline
    MyAIAgent.McpServer.Infrastructure/        # HTTP clients, KernelMemory web client
    MyAIAgent.McpServer.Presentation/          # MCP server host (SSE + POST /mcp)
    MyAIAgent.McpServer.Test/                  # xUnit tests
  Orchestrator/
    MyAIAgent.Orchestrator.Domain/             # IChatOrchestratorService, chat models, ChatSession entities
    MyAIAgent.Orchestrator.Application/        # ChatHandler, validation pipeline
    MyAIAgent.Orchestrator.Infrastructure/     # ChatOrchestratorService, ChatSessionRepository, OrchestratorDbContext
    MyAIAgent.Orchestrator.Presentation/       # ASP.NET Core host (POST /chat)
  KernelMemory/
    MyAIAgent.KernelMemory.Domain/             # DocumentRecord entity, options
    MyAIAgent.KernelMemory.Application/        # KernelMemory application extensions
    MyAIAgent.KernelMemory.Infrastructure/     # KernelMemory builder, KernelMemoryDbContext
    MyAIAgent.KernelMemory.Presentation/       # Self-hosted KernelMemory daemon
                                               # Filters/HttpAuthEndpointFilter, Filters/HttpErrorsEndpointFilter
```

**Dependency direction:** `Domain` ← `Application` ← `Infrastructure` ← `Presentation`

## Chat Session Persistence (Orchestrator)

- `ChatSession` + `ChatSessionMessage` entities stored in Cosmos DB via `OrchestratorDbContext`
- `IChatSessionRepository` / `ChatSessionRepository` — find, add, update sessions
- `ChatHandler` — looks up session by `SessionId`, appends messages, creates new session if none provided
- `ChatRequestDto(SessionId?, UserMessage)` → `ChatCommand` → `ResponseModel<ChatResponse(Reply, SessionId)>`
- `TimeProvider` injected for testable timestamps — registered as `TimeProvider.System` in `Program.cs`

## Commands

```bash
# Build the solution
dotnet build src/MyAIAgent.sln

# Run all tests
dotnet test src/MyAIAgent.McpServer.Test/MyAIAgent.McpServer.Test.csproj

# Run via Aspire (orchestrates all three services)
dotnet run --project src/MyAIAgent.All/MyAIAgent.All.csproj

# Run individual services
dotnet run --project src/MyAIAgent.McpServer.Presentation/MyAIAgent.McpServer.Presentation.csproj
dotnet run --project src/MyAIAgent.Orchestrator.Presentation/MyAIAgent.Orchestrator.Presentation.csproj
dotnet run --project src/MyAIAgent.KernelMemory.Presentation/MyAIAgent.KernelMemory.Presentation.csproj

# Format check
dotnet format --verify-no-changes
```

## Skills

| Skill | When to load |
|---|---|
| `.claude/skills/clean-architecture/SKILL.md` | Adding features, debugging dependencies, refactoring layers, new service modules |
| `.claude/skills/mcp-server/SKILL.md` | Adding or modifying MCP tools, transport config |
| `.claude/skills/mediatr-cqrs/SKILL.md` | Writing handlers, commands, queries, validators |
| `.claude/skills/kernel-memory/SKILL.md` | KernelMemory service, document upload, Q&A, RAG features |
| `.claude/skills/orchestrator/SKILL.md` | Orchestrator service, Microsoft Agents Framework, chat pipeline |
| `.claude/skills/resilience/SKILL.md` | HTTP clients, retry policies, IApiClient<T> |

## Agents

| Agent | Model | Purpose | Invoke when |
|---|---|---|---|
| `planner` | opus | Layered implementation plan | Any task touching 3+ files |
| `code-reviewer` | sonnet | C# quality & conventions review | After writing or refactoring code |
| `security-reviewer` | sonnet | Secrets, validation, safe HTTP | New external integrations, auth, user input |
| `tdd-guide` | sonnet | RED → GREEN → REFACTOR | Adding handlers, tools, validators |
| `build-error-resolver` | sonnet | Fix `dotnet build` / `dotnet test` failures | Build or test exits non-zero |
| `refactor-cleaner` | sonnet | Identify and propose code improvements | Before a PR or after adding a feature |

## Slash Commands

| Command | Purpose |
|---|---|
| `/plan` | Plan a feature before coding — waits for confirmation |
| `/tdd` | Test-driven workflow: write failing test first |
| `/code-review` | Review recent C# changes against project rules |
| `/build-fix` | Autonomously fix build and test failures |
| `/test-coverage` | 4-phase pipeline: build → tests → anti-patterns → format |

## External Services

| Service | Aspire Name | Default URL | Purpose |
|---|---|---|---|
| KernelMemory | `kernel-memory` | `http://localhost:9001` | Self-hosted RAG daemon |
| MCP Server | `mcp-server` | `http://localhost:5255` | MCP tool server |
| Orchestrator | `orchestrator` | `http://localhost:5100` | Chat API |

Config lives in `appsettings.json` per service. Never commit secrets — use `dotnet user-secrets` locally or Azure App Configuration in deployed environments.
