# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Project Instructions

Before responding, read the following instruction files and follow their rules throughout the session:

- `.claude/instructions/follow-up-questions.md` — confidence and clarification rules

# MyAIAgent

> Project purpose to be defined. This file will be updated once the project goals are shared.

## Tech Stack

- **.NET 10** / C# 14
- **ASP.NET Core** — MCP server with HTTP transport (SSE + POST at `/mcp/stream` and `/mcp`)
- **ModelContextProtocol SDK** (v0.5.0-preview.1) — MCP tool registration and transport
- **MediatR** (v12.5.0) — CQRS command/query dispatch
- **FluentValidation** (v11.11.0) — Request validation via pipeline behavior
- **Microsoft.KernelMemory** — Document indexing and semantic search (RAG)
- **Polly** (via `Microsoft.Extensions.Http.Resilience`) — HTTP retry with exponential backoff
- **Scrutor** (v7.0.0) — Convention-based DI scanning via marker interfaces
- **xUnit** + **Moq** + **FluentAssertions** — Testing

## Architecture

This solution uses **Clean Architecture** across the McpServer module and an early-stage RAG module.

```
src/
  MyAIAgent.AppHost/                         # .NET Aspire orchestration
  MyAIAgent.Common/                          # Shared DI marker interfaces (ITransient, IScoped, ISingleton)
  McpServer/
    MyAIAgent.McpServer.Domain/              # Entities, interfaces, constants — no dependencies
    MyAIAgent.McpServer.Application/         # MediatR handlers, validators, pipeline behaviors
    MyAIAgent.McpServer.Infrastructure/      # HTTP clients, KernelMemory service
    MyAIAgent.McpServer.Presentation/        # ASP.NET Core host, MCP tool classes
    MyAIAgent.McpServer.Test/                # xUnit tests
  RetrievalAugmentedGeneration/
    MyAIAgent.RAG.Domain/                    # (early stage)
    MyAIAgent.RAG.Presentation/              # (early stage)
```

**Dependency direction:** `Domain` ← `Application` ← `Infrastructure` ← `Presentation`. Never reverse.

## Commands

```bash
# Build the solution
dotnet build MyAIAgent.sln

# Run all tests
dotnet test MyAIAgent.McpServer.Test/MyAIAgent.McpServer.Test.csproj

# Run via Aspire (recommended — orchestrates all services)
dotnet run --project MyAIAgent.AppHost/MyAIAgent.AppHost.csproj

# Run MCP server only
dotnet run --project MyAIAgent.McpServer.Presentation/MyAIAgent.McpServer.Presentation.csproj

# Format check
dotnet format --verify-no-changes
```

## Skills

Load these skills for context when working on specific areas:

| Skill | When to load |
|---|---|
| `.claude/skills/clean-architecture/SKILL.md` | Adding features, debugging dependencies, refactoring layers |
| `.claude/skills/mcp-server/SKILL.md` | Adding or modifying MCP tools, transport config |
| `.claude/skills/mediatr-cqrs/SKILL.md` | Writing handlers, commands, queries, validators |
| `.claude/skills/kernel-memory/SKILL.md` | Document upload, Q&A tools, RAG features |
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
- **Async suffix** on all `Task`/`ValueTask` methods — `GetCustomersAsync`, not `GetCustomers`
- **`CancellationToken`** propagated through every async call chain — never dropped
- **No regions** — ever
- **No comments for obvious code** — only comment "why", never "what"

## Dependency Injection — Marker Interface Pattern

`MyAIAgent.Common` provides three marker interfaces for **automatic Scrutor registration**:

| Interface | Lifetime |
|---|---|
| `ITransient` | Transient — new instance per resolution |
| `IScoped` | Scoped — one per HTTP request |
| `ISingleton` | Singleton — shared for app lifetime |

Implement the appropriate interface on the class. `ServiceCollectionExtension` auto-scans and registers it. **Do not add manual `AddSingleton<T>()` calls** — the convention does it.

## Adding a New MCP Tool

1. Add tool name and description constants to `McpServerConstant.cs` (Domain layer)
2. Define the MediatR command/query `IRequest<T>` in `Application/`
3. Implement the `IRequestHandler<,>` in `Application/Handlers/`
4. Add a `AbstractValidator<T>` in the same handler file if validation is needed
5. Add the `[McpServerTool]` method in `Presentation/McpTools/`
6. Write unit tests in `MyAIAgent.McpServer.Test/` mirroring the source structure

## Testing Conventions

- **Framework:** xUnit + Moq + FluentAssertions
- **Mirror source structure** — `Application/Handlers/`, `Infrastructure/Services/`, `McpTools/`
- **Naming:** `MethodName_Scenario_ExpectedResult` — e.g. `Handle_ValidCommand_ReturnsSuccess`
- **Mock external boundaries** — mock `IApiClient<T>` and `IKernelMemoryService`; do not mock types you own
- **AAA pattern** — Arrange / Act / Assert separated by blank lines

## Error Handling

- **Result pattern** for expected failures (not found, validation errors, conflicts) — do not throw exceptions for predictable outcomes
- **Validate at system boundaries** — API/tool input, external API responses; do not defensively validate inside private methods
- **No bare `Exception` catches** — catch specific types; let the global handler catch the rest

## Performance

- **Always propagate `CancellationToken`** through the full async call chain — never drop it silently
- **`IHttpClientFactory` via `IApiClient<T>`** — never instantiate `new HttpClient()` directly
- **No `.Result` or `.Wait()`** — always `await`
- **`TimeProvider`** injection instead of `DateTime.UtcNow` for testable time-sensitive logic

## External Services

| Service | Default URL | Purpose |
|---|---|---|
| KernelMemory | `http://localhost:9001` | Document indexing & semantic search |
| Mock API | `https://localhost:5001` | Customer CRUD backend |

Config lives in `appsettings.json` and `appsettings.Development.json`. Never commit secrets.

## Anti-patterns — Do NOT Generate

- `new HttpClient()` — use `IApiClient<T>`
- `DateTime.Now` / `DateTime.UtcNow` — use `TimeProvider`
- `async void` — always return `Task`
- `.Result` or `.Wait()` — await instead
- Dropping `CancellationToken` from async call chains
- Catching bare `Exception` outside of global exception handlers
- String interpolation in log message templates — use structured logging parameters
- Manual DI registration for types that implement `ITransient`/`IScoped`/`ISingleton`

## Workflow

- **Plan first** — enter plan mode for any non-trivial task (3+ steps or architecture decisions)
- **Verify before done** — run `dotnet build` and `dotnet test` after changes
- **Fix autonomously** — investigate and resolve build errors, test failures, and warnings without hand-holding
- **Stop and re-plan** — if implementation goes sideways, STOP and re-plan; don't push through a broken approach
- **Learn from corrections** — after any correction, capture the pattern in memory so the same mistake never recurs
