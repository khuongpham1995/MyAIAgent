---
name: clean-architecture
description: >
  Clean Architecture patterns for the MyAIAgent solution. Covers layer
  responsibilities, dependency direction, DI registration, shared types,
  and how to add new features across all four layers for any of the three
  services (McpServer, Orchestrator, KernelMemory).
  Load when: adding a new feature, refactoring layers, debugging a dependency
  issue, asked about project structure, or adding a new service module.
---

# Clean Architecture (MyAIAgent)

## Overview

Three backend services, each with four layers. `MyAIAgent.Common` is a shared base extended by every service.

```
Common.Domain ← Common.Application ← Common.Infrastructure
     ↑                  ↑                      ↑
[Svc].Domain      [Svc].Application      [Svc].Infrastructure ← [Svc].Presentation
```

**Dependency direction:** Domain ← Application ← Infrastructure ← Presentation. Never reverse.

Layer and cross-service boundary rules → `.claude/rules/dotnet/architecture.md`

## Layer Responsibilities

| Layer | Responsibility | Key types |
|---|---|---|
| Domain | Entities, interfaces, constants. No external dependencies. | Records, `interface I*Service`, `*Constant` |
| Application | Business logic only. References Domain + Common.Application. | `IRequest<ResponseModel<T>>`, `IRequestHandler<,>`, `AbstractValidator<T>` |
| Infrastructure | Adapts external systems to domain interfaces. | `*Service : I*Service, IScoped`, `ApiClient<T>`, `DbContext` |
| Presentation | Composition root + HTTP surface. | `Program.cs`, `McpTools/`, `Filters/` |

## Common Layer Contents

### `MyAIAgent.Common.Domain`
`ResponseModel<T>`, `ITransient`, `IScoped`, `ISingleton`

### `MyAIAgent.Common.Application`
`ValidationBehavior<TRequest, TResponse>` — always throws `ValidationException`. See `Common.Application/Behaviors/ValidationBehavior.cs`.

### `MyAIAgent.Common.Infrastructure`
`ServiceCollectionExtension` (Scrutor scan), `RetryExtension` (Polly), `GlobalExceptionHandler` (`IExceptionHandler`), `ExceptionHandlingExtension`, `CosmosDbExtension`, `AzureAppConfigurationExtension`

Do not duplicate any of these in service projects.

## DI Registration

Marker interfaces and manual registration exceptions → `.claude/rules/dotnet/di.md`

Each service has:
- `Application/Extensions/IoCExtension.cs` — registers MediatR + FluentValidation + Scrutor scan
- `Infrastructure/Extensions/IoCExtension.cs` — registers SDK clients, HttpClients, DbContext

## Adding a Feature — Step by Step

```
1. Domain/Constants/     → add constants if needed
2. Domain/Entities/      → add entity record if new data shape required
3. Domain/Interfaces/    → add interface if new external dependency introduced
4. Application/Handlers/ → add IRequest<ResponseModel<T>> + handler + validator
5. Application/Mappers/  → add mapper if new DTO → Command mapping needed
6. Infrastructure/       → implement new interface from step 3
7. Presentation/         → add HTTP endpoint or [McpServerTool] method
8. Test/                 → add unit tests mirroring Application + Infrastructure structure
```

Mapper conventions → `.claude/rules/dotnet/mapping.md`
MCP tool registration → `.claude/rules/dotnet/mcp.md`
MediatR handler patterns → `.claude/skills/mediatr-cqrs/SKILL.md`
