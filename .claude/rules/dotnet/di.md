---
alwaysApply: true
description: >
  Dependency Injection conventions for MyAIAgent. Covers the Scrutor marker
  interface pattern, when to use manual registration, and lifetime selection.
---

# Dependency Injection

## Marker Interface Pattern

`MyAIAgent.Common.Domain` provides three marker interfaces. Implement the correct one — Scrutor auto-registers it via `ServiceCollectionExtension` in `Common.Infrastructure`. No manual `AddScoped/AddSingleton/AddTransient` call needed.

| Interface | Lifetime | Use for |
|---|---|---|
| `ITransient` | Per-resolution | Stateless utilities, lightweight services |
| `IScoped` | Per HTTP request | Services that depend on `DbContext`, session-aware services |
| `ISingleton` | Application lifetime | HTTP clients, caches, stateless long-lived services |

## When to Register Manually

Manual registration is only needed for types that **cannot implement marker interfaces**:

- External SDK clients (`MemoryWebClient`, `OpenAIClient`, etc.)
- `HttpClient` via `AddHttpClient<TInterface, TImpl>()`
- MediatR + FluentValidation — registered once in Application `IoCExtension`
- `DbContext` via `AddDbContext<T>()`
- `TimeProvider.System` — framework type

## Anti-patterns

- Never add manual `AddScoped/AddSingleton/AddTransient` for a type that already implements a marker interface
- Never give a `DbContext`-dependent service the `ISingleton` lifetime — the scoped `DbContext` would be captured
- Never implement multiple lifetime markers on the same class — the registration is ambiguous
