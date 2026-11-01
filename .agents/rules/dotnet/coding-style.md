---
alwaysApply: true
description: >
  C# 14 / .NET 10 coding style conventions for MyAIAgent. Covers file
  organization, type declarations, expressions, and naming.
  See also: architecture.md, di.md, mapping.md, mcp.md in this folder.
---

# .NET Coding Style

## File Organization

- **File-scoped namespaces always** — block-scoped namespaces waste indentation
- **One type per file** — file name must match the type name exactly
- **Member order:** constants → fields → constructors → properties → public methods → private methods

## Type Declarations

- **Primary constructors for DI** — no manual `_field = field` assignments or backing fields
- **Records for DTOs, commands, queries, and response models** — immutable by default
- **`sealed` on all classes not designed for inheritance** — communicates intent, enables JIT devirtualization
- **`internal` by default** — `public` only when consumed across project boundaries

## Expressions and Patterns

- **Collection expressions** (`[item1, item2]`) over `new List<T> { }` constructor calls
- **Pattern matching and switch expressions** over if-else chains
- **Expression bodies** (`=>`) for single-expression methods and properties

## Naming

- **`var`** for obvious types; explicit types when the type isn't clear from context
- **PascalCase** for public members, types, namespaces, methods
- **camelCase** for local variables and parameters
- **No `_` prefix** on private fields when using primary constructors
- **No regions. Ever.**

## Minimal API Modules

- Gateway/Orchestrator application routes use `/api` for REST and `/sse` for chat streaming.
  Map each prefix through its own authorized group; do not change HTTP verbs or SSE events.
  Health, documentation, MCP protocol and KernelMemory SDK routes are explicit exceptions.
- Group maintained routes by module in endpoint extension classes.
- Use `IEndpointRouteBuilder Map[Module]Endpoints(this IEndpointRouteBuilder endpoints, ...)`
  and return the incoming builder for chaining. Add explicit parameters only for required settings or owner resolution.
- Keep middleware/hosting in startup helpers. Apply group authorization before mapping protected modules.
- Preserve paths, verbs, operation names, filters, response metadata, and cancellation when extracting routes.
