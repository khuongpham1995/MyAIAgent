---
alwaysApply: true
description: >
  Defines when Claude should auto-load each skill for MyAIAgent.
  Skills live in .claude/skills/ and provide deep domain context.
---

# Skill Triggers

Load a skill when the task matches — do not wait for the user to ask.

## Trigger Table

| Skill | Auto-load when |
|---|---|
| `clean-architecture` | Adding features, debugging layer dependencies, refactoring, structure questions |
| `mcp-server` | Adding or modifying MCP tools, configuring transport, tool registration |
| `mediatr-cqrs` | Writing handlers, commands, queries, validators, pipeline behaviors |
| `kernel-memory` | Document upload tools, Q&A tools, RAG features, KernelMemory service |
| `resilience` | HTTP clients, retry policies, `IApiClient<T>`, external service calls |

## File-based Auto-load Rules

| File touched | Load skill |
|---|---|
| `Presentation/McpTools/**` | `mcp-server` |
| `Application/Handlers/**` | `mediatr-cqrs` |
| `Application/Commands/**` or `Application/Queries/**` | `mediatr-cqrs` |
| `Application/Mappers/**` | `mediatr-cqrs` (mapper sits at Application boundary) |
| `Infrastructure/ApiClients/**` | `resilience` |
| `Infrastructure/Services/KernelMemoryService*` | `kernel-memory` |
| 3+ layers touched in one task | `clean-architecture` |
