---
alwaysApply: true
description: >
  Mapper conventions for MyAIAgent. Covers placement, naming, method signatures,
  and what to map at each layer boundary.
---

# Mapping Rules

## Where Mappers Live

Mappers belong in **`Application/Mappers/`** — they translate between Presentation DTOs and Application Commands/Queries. This is the only layer boundary where mapping is needed.

- `Presentation DTO → Application Command/Query` — use a mapper
- `Application → Presentation Response` — return `ResponseModel<T>` directly; mapper optional
- `Infrastructure → Domain` — domain interfaces handle this

Never put mappers in `Presentation/`, `Infrastructure/`, or `Domain/`.

## Naming Convention

| Item | Convention | Example |
|---|---|---|
| File | `[SourceType]Mapper.cs` | `ChatCommandMapper.cs` |
| Class | `static`, same name as file | `public static class ChatCommandMapper` |
| Method | `To[TargetType]()` extension on source | `ToCommand()`, `ToQuery()`, `ToResponse()` |

## Rules

- `static` class — no constructor, no DI
- Extension method on the **source** type (the DTO)
- Pure function — no side effects, no external calls
- One mapper class per source type
- Expression body for single-line mappings

## Usage

Call the mapper in `Program.cs` when dispatching to MediatR: `request.ToCommand()`.

## Anti-patterns

- Never map inside a handler — mixes mapping with business logic
- Never use AutoMapper or reflection-based mapping — hidden coupling, runtime errors
- Never construct a command inline in an endpoint — always use the `Mappers/` extension method
- Never put mapping logic in domain entities — domain must not reference Application types
