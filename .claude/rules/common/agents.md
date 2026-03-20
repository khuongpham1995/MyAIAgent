## Core Principles

1. **Delegate proactively** — invoke agents without waiting for the user to ask
2. **Plan before coding** — use `planner` for any non-trivial feature (3+ steps)
3. **Review every change** — invoke `code-reviewer` after writing or refactoring C# code
4. **Test-driven by default** — use `tdd-guide` for new handlers, tools, and validators
5. **Fix builds autonomously** — use `build-error-resolver` before surfacing errors to the user

## Agent Roster

| Agent | File | When to invoke |
|---|---|---|
| `planner` | `.claude/agents/planner.md` | Complex features, architectural decisions, new modules |
| `code-reviewer` | `.claude/agents/code-reviewer.md` | After writing or refactoring any C# code |
| `tdd-guide` | `.claude/agents/tdd-guide.md` | Adding handlers, validators, tool methods |
| `build-error-resolver` | `.claude/agents/build-error-resolver.md` | `dotnet build` or `dotnet test` fails |
| `security-reviewer` | `.claude/agents/security-reviewer.md` | External HTTP, auth, secrets, user input |

## Standard Development Workflow

```
1. Plan         → planner agent
2. TDD          → tdd-guide agent (write failing test first)
3. Implement    → write production code (skills: clean-architecture, mcp-server, mediatr-cqrs)
4. Review       → code-reviewer agent
5. Verify       → /verify (build + tests + anti-patterns + format)
6. Commit       → conventional commit
```

## Parallel Execution

Run agents in parallel when tasks are independent:

```
User: "Add a search_customer_by_email tool"

Parallel:
  → planner (identify affected files + plan)
  → tdd-guide (write failing test)
Then:
  → implement handler + tool method
  → code-reviewer
  → /verify
```

## Agent Boundaries

- **planner** — read-only. Produces a plan. No code changes.
- **code-reviewer** — read-only. Reports findings. No code changes.
- **tdd-guide** — writes test files only. Does not touch production code.
- **build-error-resolver** — reads errors, writes targeted fixes to failing files only.
- **security-reviewer** — read-only. Reports findings with severity (CRITICAL / HIGH / MEDIUM / LOW).
