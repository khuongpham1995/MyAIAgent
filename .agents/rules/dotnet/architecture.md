---
alwaysApply: true
description: >
  Clean Architecture layer boundaries and dependency rules for MyAIAgent.
  Covers allowed references per layer, shared Common layers, and cross-service rules.
---

# Architecture — Layer Boundaries

Dependency direction is inward: **Domain ← Application ← Infrastructure ← Presentation**. Never reverse.

| Layer | Project | Allowed dependencies |
|---|---|---|
| Domain | `MyAIAgent.[Svc].Domain` | `Common.Domain` only |
| Application | `MyAIAgent.[Svc].Application` | Domain + `Common.Application` |
| Infrastructure | `MyAIAgent.[Svc].Infrastructure` | Application + Domain + `Common.Infrastructure` |
| Presentation | `MyAIAgent.[Svc].Presentation` | Application + Infrastructure + Domain |
| Shared | `MyAIAgent.Common.*` | No service project references |

## Cross-Service Rules

- Services communicate via **HTTP only** — never via project references
- `MyAIAgent.Common.Domain`, `Common.Application`, and `Common.Infrastructure` are the shared projects across service boundaries
- Never add a `<ProjectReference>` from one service to another service's project

## Common Layer Mapping

| Service layer | References Common layer |
|---|---|
| `[Service].Domain` | `Common.Domain` |
| `[Service].Application` | `Common.Application` (gets `Common.Domain` transitively) |
| `[Service].Infrastructure` | `Common.Infrastructure` (gets `Common.Domain` transitively) |

## Type Placement Conventions

| Type | Layer | Examples |
|---|---|---|
| Entities (EF Core) | Domain | `ChatSession`, `AgentDefinition` |
| Domain entities and value objects | Domain | `ChatSession`, `DocumentRecord` |
| Feature contracts and projections | Application/{Feature} | `ChatRequest`, `AgentSummary`, `ChatResponse` |
| Inbound HTTP DTOs | Application/{Feature} | `ChatRequestDto`, `CreateAgentRequestDto` |
| Commands / Queries | Application | `ChatCommand`, `CreateAgentCommand` |
| Application interfaces | Application/Common/Interfaces | `IAgentBuilderService` |
| Ports consumed by Application | Application/Common/Interfaces | `IChatOrchestratorService`, `IAgentRepository`, `IUnitOfWork` |
| Infrastructure interfaces (internal-only) | Same file as implementation | `IAgentHelper` in `AgentHelper.cs` |

> **Rule:** Inbound HTTP request DTOs and feature result contracts belong in `[Service].Application/{Feature}/`. They are mapped to Commands/Queries at the Application boundary. Domain must not hold HTTP-contract types.

Each service keeps the existing `Presentation` project name. Application and Infrastructure
register services from a root `IoCExtension.cs`. Keep the shared base entity and lifetime
markers in `Common.Domain/Common/`; place repositories under `Infrastructure/Data/Repositories/`
and external adapters under `Infrastructure/Services/` when a service needs them. Add only
folders backed by actual code; tenant databases, migrations, controllers, and audit
interceptors are optional components, not required scaffolding.

> **Rule:** If an Infrastructure interface is consumed only within the Infrastructure layer, declare it in the same `.cs` file as its implementation. Promote it to Application when a handler needs the abstraction.

## Anti-patterns

- Never inject a type from a higher layer into a lower one (e.g., a Presentation type into a handler)
- Never bypass MediatR by calling Infrastructure directly from Presentation endpoints
- Never add a `<ProjectReference>` between sibling services — communicate via HTTP only
- Never place inbound HTTP DTOs in the Domain layer — they belong in Application/{Feature}/
