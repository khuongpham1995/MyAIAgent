---
alwaysApply: true
description: >
  Dependency Injection conventions for MyAIAgent. Covers the Scrutor marker
  interface pattern, when to use manual registration, and lifetime selection.
---

# Dependency Injection

## Marker Interface Pattern

`MyAIAgent.Common.Domain` provides three marker interfaces. Implement the correct one — Scrutor auto-registers it via `ServiceCollectionExtension` in `Common.Application`. No manual `AddScoped/AddSingleton/AddTransient` call needed.

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

Framework builder callbacks are a narrow exception when registration must be conditional,
must preserve multiple implementations of one framework interface in a specific order,
or requires per-instance constructor arguments. Register through the framework's own
service collection so its internal provider receives the services. Document why the
marker scanner is unsuitable at the registration site, and test resolution through
the application's actual builder path, including implementation precedence.

## Anti-patterns

- Never add manual `AddScoped/AddSingleton/AddTransient` for a type that already implements a marker interface
- Never give a `DbContext`-dependent service the `ISingleton` lifetime — the scoped `DbContext` would be captured
- Never implement multiple lifetime markers on the same class — the registration is ambiguous
