# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Project Instructions

Before responding, read the following instruction files and follow their rules throughout the session:

- `.claude/instructions/follow-up-questions.md` — confidence and clarification rules

# MyAIAgent

An AI agent backend with three services: **Orchestrator** (Microsoft Agents Framework), **KernelMemory** (RAG), and **MCP Server** (tools). All services follow Clean Architecture.

## Tech Stack

- **.NET 10** / C# 14
- **.NET Aspire** — multi-service orchestration (`MyAIAgent.AppHost`)
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

Three services, each with Clean Architecture. One shared Common base — also structured as Clean Architecture layers.

```
src/
  MyAIAgent.AppHost/                         # .NET Aspire orchestration
  Common/
    MyAIAgent.Common.Domain/                 # ResponseModel<T>, ITransient/IScoped/ISingleton
    MyAIAgent.Common.Application/            # ValidationBehavior<TRequest,TResponse>, shared app extensions
    MyAIAgent.Common.Infrastructure/         # ServiceCollectionExtension (Scrutor), RetryExtension (Polly)
  McpServer/
    MyAIAgent.McpServer.Domain/              # Entities, interfaces, MCP constants
    MyAIAgent.McpServer.Application/         # MediatR handlers, validators, pipeline
    MyAIAgent.McpServer.Infrastructure/      # HTTP clients, KernelMemory web client
    MyAIAgent.McpServer.Presentation/        # MCP server host (SSE + POST /mcp)
    MyAIAgent.McpServer.Test/                # xUnit tests
  Orchestrator/
    MyAIAgent.Orchestrator.Domain/           # IChatOrchestratorService, chat models
    MyAIAgent.Orchestrator.Application/      # ChatHandler, validation pipeline
    MyAIAgent.Orchestrator.Infrastructure/   # ChatOrchestratorService (Agents Framework)
    MyAIAgent.Orchestrator.Presentation/     # ASP.NET Core host (POST /chat)
  KernelMemory/
    MyAIAgent.KernelMemory.Domain/           # IDocumentService, document models
    MyAIAgent.KernelMemory.Application/      # UploadDocumentHandler, AskDocumentQuestionHandler
    MyAIAgent.KernelMemory.Infrastructure/   # KernelMemory builder configuration
    MyAIAgent.KernelMemory.Presentation/     # Self-hosted KernelMemory daemon
```

**Dependency direction (per service):** `Domain` ← `Application` ← `Infrastructure` ← `Presentation`

`MyAIAgent.Common` is referenced by Application and Infrastructure layers of all services.

## Commands

```bash
# Build the solution
dotnet build src/MyAIAgent.sln

# Run all tests
dotnet test src/MyAIAgent.McpServer.Test/MyAIAgent.McpServer.Test.csproj

# Run via Aspire (orchestrates all three services)
dotnet run --project src/MyAIAgent.AppHost/MyAIAgent.AppHost.csproj

# Run individual services
dotnet run --project src/MyAIAgent.McpServer.Presentation/MyAIAgent.McpServer.Presentation.csproj
dotnet run --project src/MyAIAgent.Orchestrator.Presentation/MyAIAgent.Orchestrator.Presentation.csproj
dotnet run --project src/MyAIAgent.KernelMemory.Presentation/MyAIAgent.KernelMemory.Presentation.csproj

# Format check
dotnet format --verify-no-changes
```

## Skills

Load these skills for context when working on specific areas:

| Skill | When to load |
|---|---|
| `.claude/skills/clean-architecture/SKILL.md` | Adding features, debugging dependencies, refactoring layers, new service modules |
| `.claude/skills/mcp-server/SKILL.md` | Adding or modifying MCP tools, transport config |
| `.claude/skills/mediatr-cqrs/SKILL.md` | Writing handlers, commands, queries, validators |
| `.claude/skills/kernel-memory/SKILL.md` | KernelMemory service, document upload, Q&A, RAG features |
| `.claude/skills/orchestrator/SKILL.md` | Orchestrator service, Microsoft Agents Framework, chat pipeline |
| `.claude/skills/resilience/SKILL.md` | HTTP clients, retry policies, IApiClient<T> |

## Agents

Specialized agents live in `.claude/agents/`. See `AGENTS.md` for routing guide.

| Agent | Purpose | Invoke when |
|---|---|---|
| `planner` | Layered implementation plan | Any task touching 3+ files |
| `code-reviewer` | C# quality & conventions review | After writing or refactoring code |
| `tdd-guide` | RED → GREEN → REFACTOR | Adding handlers, tools, validators |
| `build-error-resolver` | Fix `dotnet build` / `dotnet test` failures | Build or test exits non-zero |
| `security-reviewer` | Secrets, validation, safe HTTP | New external integrations, auth, user input |

## Commands

| Command | Purpose |
|---|---|
| `/plan` | Plan a feature before coding — waits for confirmation |
| `/tdd` | Test-driven workflow: write failing test first |
| `/code-review` | Review recent C# changes against project rules |
| `/build-fix` | Autonomously fix build and test failures |
| `/verify` | 4-phase pipeline: build → tests → anti-patterns → format |
| `/add-tool` | Scaffold a complete MCP tool across all layers |
| `/add-handler` | Scaffold a MediatR handler + optional validator + test |

## Coding Standards

Follow the rules in `.claude/rules/` (always loaded):

- **Primary constructors** for DI — no manual field assignments
- **Records** for DTOs, commands, queries, and value objects
- **`sealed`** on all classes not designed for inheritance
- **File-scoped namespaces** — always
- **`var`** for obvious types; explicit types when the type isn't clear from context
- **Async suffix** on all `Task`/`ValueTask` methods
- **`CancellationToken`** propagated through every async call chain — never dropped
- **No regions** — ever
- **No comments for obvious code** — only comment "why", never "what"

## Dependency Injection — Marker Interface Pattern

`MyAIAgent.Common.Domain` provides three marker interfaces for **automatic Scrutor registration**:

| Interface | Lifetime |
|---|---|
| `ITransient` | Transient — new instance per resolution |
| `IScoped` | Scoped — one per HTTP request |
| `ISingleton` | Singleton — shared for app lifetime |

Implement the appropriate interface on the class. `ServiceCollectionExtension` (in `Common.Infrastructure`) auto-scans and registers it. **Do not add manual `AddSingleton<T>()` calls** for types that implement these interfaces.

## Shared Layers — `MyAIAgent.Common.*`

| Project | References | Contains |
|---|---|---|
| `MyAIAgent.Common.Domain` | None | `ResponseModel<T>`, `ITransient`, `IScoped`, `ISingleton` |
| `MyAIAgent.Common.Application` | Common.Domain | `ValidationBehavior<TRequest,TResponse>`, shared application extensions |
| `MyAIAgent.Common.Infrastructure` | Common.Domain | `ServiceCollectionExtension` (Scrutor scan), `RetryExtension` (Polly) |

Service layers reference their counterpart:
- Service **Domain** → `Common.Domain`
- Service **Application** → `Common.Application` (gets `Common.Domain` transitively)
- Service **Infrastructure** → `Common.Infrastructure` (gets `Common.Domain` transitively)

## Adding a New MCP Tool

1. Add tool name and description constants to `McpServerConstant.cs` (Domain layer)
2. Define the MediatR command/query `IRequest<ResponseModel<T>>` in `Application/`
3. Implement the `IRequestHandler<,>` in `Application/Handlers/`
4. Add a `AbstractValidator<T>` in the same handler file if validation is needed
5. Add the `[McpServerTool]` method in `Presentation/McpTools/`
6. Write unit tests in `MyAIAgent.McpServer.Test/` mirroring the source structure

## Testing Conventions

- **Framework:** xUnit + Moq + FluentAssertions
- **Mirror source structure** — `Application/Handlers/`, `Infrastructure/Services/`, `McpTools/`
- **Naming:** `MethodName_Scenario_ExpectedResult`
- **Mock external boundaries** — `IApiClient<T>`, `IKernelMemoryService`, `IChatOrchestratorService`; do not mock types you own
- **AAA pattern** — Arrange / Act / Assert separated by blank lines

## Error Handling

- **Result pattern** (`ResponseModel<T>`) for expected failures — do not throw for predictable outcomes
- **Validate at system boundaries** — API/tool input, external API responses
- **No bare `Exception` catches** — catch specific types; let the global handler catch the rest

## Performance

- **Always propagate `CancellationToken`** through the full async call chain
- **`IHttpClientFactory` via `IApiClient<T>`** — never `new HttpClient()`
- **No `.Result` or `.Wait()`** — always `await`
- **`TimeProvider`** injection instead of `DateTime.UtcNow`

## External Services

| Service | Aspire Name | Default URL | Purpose |
|---|---|---|---|
| KernelMemory | `kernel-memory` | `http://localhost:9001` | Self-hosted RAG daemon |
| MCP Server | `mcp-server` | `http://localhost:5255` | MCP tool server |
| Orchestrator | `orchestrator` | `http://localhost:5100` | Chat API |

Config lives in `appsettings.json` per service. Never commit secrets — use `dotnet user-secrets`.

## Anti-patterns — Do NOT Generate

- `new HttpClient()` — use `IApiClient<T>`
- `DateTime.Now` / `DateTime.UtcNow` — use `TimeProvider`
- `async void` — always return `Task`
- `.Result` or `.Wait()` — await instead
- Dropping `CancellationToken` from async call chains
- Catching bare `Exception` outside of global exception handlers
- String interpolation in log message templates — use structured logging parameters
- Manual DI registration for types that implement `ITransient`/`IScoped`/`ISingleton`
- Cross-service project references — services communicate via HTTP only

## Workflow

- **Plan first** — enter plan mode for any non-trivial task (3+ steps or architecture decisions)
- **Verify before done** — run `dotnet build` and `dotnet test` after changes
- **Fix autonomously** — investigate and resolve build errors, test failures, and warnings without hand-holding
- **Stop and re-plan** — if implementation goes sideways, STOP and re-plan; don't push through a broken approach
- **Learn from corrections** — after any correction, capture the pattern in memory so the same mistake never recurs
