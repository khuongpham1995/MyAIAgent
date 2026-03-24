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
- `MyAIAgent.Common` is the only shared project reference across service boundaries
- Never add a `<ProjectReference>` from one service to another service's project

## Common Layer Mapping

| Service layer | References Common layer |
|---|---|
| `[Service].Domain` | `Common.Domain` |
| `[Service].Application` | `Common.Application` (gets `Common.Domain` transitively) |
| `[Service].Infrastructure` | `Common.Infrastructure` (gets `Common.Domain` transitively) |

## Anti-patterns

- Never inject a type from a higher layer into a lower one (e.g., a Presentation type into a handler)
- Never bypass MediatR by calling Infrastructure directly from Presentation endpoints
- Never add a `<ProjectReference>` between sibling services — communicate via HTTP only
