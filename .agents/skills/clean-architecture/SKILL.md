---
name: clean-architecture
description: >
  Clean Architecture patterns for the MyAIAgent solution. Covers layer
  responsibilities, dependency direction, DI registration, shared types,
  and how to add new features across all four layers for any of the four
  services (McpServer, Orchestrator, KernelMemory, MainSubSys).
  Load when: adding a new feature, refactoring layers, debugging a dependency
  issue, asked about project structure, or adding a new service module.
---

# Clean Architecture (MyAIAgent)

## Overview

Four backend services, each with four layers. `MyAIAgent.Common` is a shared base extended by every service.

```
Common.Domain ← Common.Application ← Common.Infrastructure
     ↑                  ↑                      ↑
[Svc].Domain      [Svc].Application      [Svc].Infrastructure ← [Svc].Presentation
```

**Dependency direction:** Domain ← Application ← Infrastructure ← Presentation. Never reverse.

Layer and cross-service boundary rules → `.agents/rules/dotnet/architecture.md`

## Layer Responsibilities

| Layer | Responsibility | Key types |
|---|---|---|
| Domain | Entities, models, constants. No external dependencies. | Records, `*Constant` |
| Application | Business logic and ports for external dependencies. References Domain + Common.Application. | `IRequest<ResponseModel<T>>`, `IRequestHandler<,>`, `AbstractValidator<T>`, `interface I*Service` |
| Infrastructure | Adapts external systems to domain interfaces. | `*Service : I*Service, IScoped`, `PageFetcher`, `DbContext` |
| Presentation | Composition root + HTTP surface. | `Program.cs`, `McpTools/`, `Filters/` |

## Common Layer Contents

### `MyAIAgent.Common.Domain`
`BaseEntity<TKey>`, `ResponseModel<T>`, `ITransient`, `IScoped`, `ISingleton`

### `MyAIAgent.Common.Application`
`ValidationBehavior<TRequest, TResponse>` — always throws `ValidationException`. See `Common.Application/Behaviors/ValidationBehavior.cs`.

### `MyAIAgent.Common.Infrastructure`
`ServiceCollectionExtension` (Scrutor scan), `RetryExtension` (Polly), `GlobalExceptionHandler` (`IExceptionHandler`), `ExceptionHandlingExtension`, `CosmosDbExtension`, `AzureAppConfigurationExtension`

Do not duplicate any of these in service projects.

## DI Registration

Marker interfaces and manual registration exceptions → `.agents/rules/dotnet/di.md`

Each service has:
- `Application/Extensions/IoCExtension.cs` — registers MediatR + FluentValidation + Scrutor scan
- `Infrastructure/Extensions/IoCExtension.cs` — registers SDK clients, HttpClients, DbContext

## Adding a Feature — Step by Step

```
1. Domain/Constants/     → add constants if needed
2. Domain/Entities/      → add an entity inheriting BaseEntity<TKey> if new data shape required
3. Application/Interfaces/ → add a port if a handler needs an external dependency
4. Application/{Feature}/Commands/{RequestName}/ or Queries/{RequestName}/
   → add the request, handler and validator as separate files in one folder
5. Application/Common/Mappings/ → add a Mapster IRegister mapping for each application contract pair
6. Infrastructure/       → implement new port from step 3
7. Presentation/         → add HTTP endpoint or [McpServerTool] method
8. Test/                 → add unit tests mirroring Application + Infrastructure structure
```

Mapper conventions → `.agents/rules/dotnet/mapping.md`
MCP tool registration → `.agents/rules/dotnet/mcp.md`
MediatR handler patterns → `.agents/skills/mediatr-cqrs/SKILL.md`
