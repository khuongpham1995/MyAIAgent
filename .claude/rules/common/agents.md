## Core Principles

1. **Delegate proactively** — invoke agents without waiting for the user to ask
2. **Plan before coding** — use `planner` for any non-trivial feature (3+ steps)
3. **Review every change** — invoke `code-reviewer` after writing or refactoring C# code
4. **Test-driven by default** — use `tdd-guide` for new handlers, tools, and validators
5. **Fix builds autonomously** — use `build-error-resolver` before surfacing errors to the user

## Agent Roster

| Agent | File | Model | Boundary |
|---|---|---|---|
| `planner` | `.claude/agents/planner.md` | `claude-opus-4-6` | Read-only — produces plan, no code changes |
| `code-reviewer` | `.claude/agents/code-reviewer.md` | `claude-sonnet-4-6` | Read-only — reports findings, no code changes |
| `security-reviewer` | `.claude/agents/security-reviewer.md` | `claude-sonnet-4-6` | Read-only — severity report only |
| `tdd-guide` | `.claude/agents/tdd-guide.md` | `claude-sonnet-4-6` | Writes test files only |
| `build-error-resolver` | `.claude/agents/build-error-resolver.md` | `claude-sonnet-4-6` | Targeted fixes to failing files only |
| `refactor-cleaner` | `.claude/agents/refactor-cleaner.md` | `claude-sonnet-4-6` | Proposes changes, waits for confirmation |

## Standard Development Workflow

```
1. Plan         → planner agent
2. TDD          → tdd-guide agent (write failing test first)
3. Implement    → write production code
4. Review       → code-reviewer agent
5. Verify       → /test-coverage (build + tests + anti-patterns + format)
6. Commit       → conventional commit
```

## Parallel Execution

Run agents in parallel when tasks are independent by launching multiple Agent tool calls in a single response with `run_in_background: true`. Wait for all to complete before proceeding to dependent steps.

```
Single response — launch in parallel (run_in_background: true):
  → planner (identify affected files + plan)
  → tdd-guide (write failing test)

Wait for both, then sequentially:
  → implement handler
  → code-reviewer
  → /test-coverage
```

## Agent Boundaries

- **planner** — read-only. Produces a plan. No code changes.
- **code-reviewer** — read-only. Reports findings. No code changes.
- **security-reviewer** — read-only. Reports findings with severity (CRITICAL / HIGH / MEDIUM / LOW).
- **tdd-guide** — writes test files only. Does not touch production code.
- **build-error-resolver** — reads errors, writes targeted fixes to failing files only.
- **refactor-cleaner** — proposes refactors. Waits for user confirmation before applying changes.
