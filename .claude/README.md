# .claude — Claude Code Configuration

This directory configures Claude Code for the MyAIAgent .NET solution.

## Structure

| Directory | Purpose |
|---|---|
| `agents/` | Specialized sub-agents invoked for specific tasks |
| `commands/` | Slash commands (`/plan`, `/tdd`, `/code-review`, etc.) |
| `hooks/` | Shell scripts triggered on tool use events |
| `instructions/` | Always-loaded instruction and standards files |
| `rules/common/` | Cross-cutting rules (git, testing, security, workflow) |
| `rules/dotnet/` | .NET / C#-specific coding standards and architecture |
| `skills/` | Domain context loaded on demand for specific areas |
| `docs/` | Reference documentation for agents and task routing |

## Skills

| Skill | Load when |
|---|---|
| `clean-architecture` | Adding features, debugging layer dependencies, new service modules |
| `mcp-server` | Adding/modifying MCP tools, transport config |
| `mediatr-cqrs` | Handlers, commands, queries, validators, pipeline behaviors |
| `kernel-memory` | KernelMemory daemon, document ingestion, Q&A, RAG |
| `orchestrator` | Orchestrator service, Microsoft Agents Framework, chat pipeline |
| `resilience` | HTTP clients, retry policies, `IApiClient<T>` |

## Quick Reference

| When | Invoke |
|---|---|
| New feature (3+ files) | `/plan` |
| New handler or tool | `/tdd` |
| After writing code | `/code-review` |
| Build or test failure | `/build-fix` |
| Before PR | `/test-coverage` |

## Services

The solution has three backend services plus shared infrastructure:

```
MyAIAgent.Common          ← shared base (ResponseModel<T>, DI marker interfaces)
MyAIAgent.McpServer.*     ← MCP tool server (Clean Architecture, 4 layers)
MyAIAgent.Orchestrator.*  ← Chat API / AI agent (Clean Architecture, 4 layers)
MyAIAgent.KernelMemory.*  ← RAG / KernelMemory daemon (Clean Architecture, 4 layers)
MyAIAgent.AppHost         ← .NET Aspire orchestration
```
