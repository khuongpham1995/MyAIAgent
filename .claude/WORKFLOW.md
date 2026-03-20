# Development Workflow

## Standard Flow

```
1. Plan         → /plan          (planner agent — identify files, risks, steps)
2. TDD          → /tdd           (tdd-guide agent — write failing test first)
3. Implement    → write code     (skills: clean-architecture, mcp-server, mediatr-cqrs)
4. Review       → /code-review   (code-reviewer agent)
5. Verify       → /test-coverage (build + tests + anti-patterns + format)
6. Commit       → conventional commit
```

## Agent Delegation

| Task | Agent |
|---|---|
| Complex feature, 3+ files | `planner` |
| New handler / validator / tool | `tdd-guide` |
| After any code change | `code-reviewer` |
| Build or test failure | `build-error-resolver` |
| New external HTTP, auth, user input | `security-reviewer` |
| Code smell or duplication | `refactor-cleaner` |

## Parallel Execution

Run agents in parallel for independent tasks:

```
User: "Add a search_customer_by_email tool"

Parallel:
  → planner (plan the implementation)
  → tdd-guide (write failing test)
Then:
  → implement handler + tool
  → code-reviewer
  → /test-coverage
```

## Agent Boundaries

- **planner** — read-only. Produces a plan. No code changes.
- **code-reviewer** — read-only. Reports findings. No code changes.
- **tdd-guide** — writes test files only. Does not touch production code.
- **build-error-resolver** — reads errors, writes targeted fixes to failing files only.
- **security-reviewer** — read-only. Reports findings with severity levels.
- **refactor-cleaner** — proposes refactors, waits for confirmation before applying.
