# Development Workflow

## Standard Flow

```
1. Plan         → /plan          (planner agent — identify files, risks, steps)
2. TDD          → /tdd           (tdd-guide agent — write failing test first)
3. Implement    → write code     (skills: clean-architecture, mcp-server, mediatr-cqrs, orchestrator, kernel-memory)
4. Review       → /full-review   (code-reviewer + security-reviewer + build-error-resolver in parallel)
5. Verify       → /test-coverage (build + tests + anti-patterns + format)
6. Commit       → conventional commit
```

## Service Responsibilities

| Service | Entry point | When to touch |
|---|---|---|
| **Orchestrator** | `MyAIAgent.Orchestrator.*` | Chat pipeline, AI agent, tool dispatch changes |
| **KernelMemory** | `MyAIAgent.KernelMemory.*` | RAG config, document ingestion, embedding provider changes |
| **MCP Server** | `MyAIAgent.McpServer.*` | Adding/modifying tools exposed to LLMs |
| **Common** | `MyAIAgent.Common` | Shared types (`ResponseModel<T>`), DI marker interfaces |

## Agent Delegation

| Task | Agent | Model |
|---|---|---|
| Complex feature, 3+ files | `planner` | opus |
| New handler / validator / tool | `tdd-guide` | sonnet |
| After any code change | `code-reviewer` | sonnet |
| Build or test failure | `build-error-resolver` | sonnet |
| New external HTTP, auth, user input | `security-reviewer` | sonnet |
| Code smell or duplication | `refactor-cleaner` | sonnet |

## Skill Triggers

| File touched | Load skill |
|---|---|
| `Orchestrator/**` | `orchestrator` |
| `KernelMemory/**` | `kernel-memory` |
| `McpServer/Presentation/McpTools/**` | `mcp-server` |
| `*/Application/Handlers/**` | `mediatr-cqrs` |
| `*/Infrastructure/ApiClients/**` | `resilience` |
| 3+ layers across any service | `clean-architecture` |

## Parallel Execution

Run agents in parallel for independent tasks:

```
User: "Add an ask_product_catalog MCP tool"

Parallel:
  → planner (plan the implementation across all layers)
  → tdd-guide (write failing test)
Then:
  → implement handler + tool
  → code-reviewer
  → /test-coverage
```

## Cross-Service Changes

When a change touches multiple services (e.g. adding a new tool that the Orchestrator also calls):

```
1. planner  — map the full dependency chain across services
2. Implement service-by-service in dependency order:
     KernelMemory → MCP Server → Orchestrator
3. code-reviewer on each service
4. /test-coverage for the full solution
```

## Agent Boundaries

- **planner** — read-only. Produces a plan. No code changes.
- **code-reviewer** — read-only. Reports findings. No code changes.
- **tdd-guide** — writes test files only. Does not touch production code.
- **build-error-resolver** — reads errors, writes targeted fixes to failing files only.
- **security-reviewer** — read-only. Reports findings with severity levels.
- **refactor-cleaner** — proposes refactors, waits for confirmation before applying.
