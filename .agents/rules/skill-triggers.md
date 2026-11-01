---
alwaysApply: true
description: >
  Defines when either assistant should load each shared skill for MyAIAgent.
  Skills live in .agents/skills/ and provide deep domain context.
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
| `resilience` | HTTP clients, retry policies, typed clients, external service calls |

## File-based Auto-load Rules

| File touched | Load skill |
|---|---|
| `Presentation/McpTools/**` | `mcp-server` |
| `Application/**/Commands/**` or `Application/**/Queries/**` | `mediatr-cqrs` |
| `Application/Common/Mappings/**` | `mediatr-cqrs` (application contract mappings) |
| `src/Services/*/MyAIAgent.*.Infrastructure/Services/**` | `resilience` |
| `Infrastructure/Services/KernelMemoryService*` | `kernel-memory` |
| 3+ layers touched in one task | `clean-architecture` |
