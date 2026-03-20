# Agent Collaboration Framework

Reference guide for multi-agent coordination in MyAIAgent.

## Agent Roster

| Agent | File | Boundary |
|---|---|---|
| `planner` | `agents/planner.md` | Read-only — produces plan, no code changes |
| `tdd-guide` | `agents/tdd-guide.md` | Writes test files only |
| `code-reviewer` | `agents/code-reviewer.md` | Read-only — reports findings, no code changes |
| `build-error-resolver` | `agents/build-error-resolver.md` | Targeted fixes to failing files only |
| `security-reviewer` | `agents/security-reviewer.md` | Read-only — severity report only |
| `refactor-cleaner` | `agents/refactor-cleaner.md` | Proposes changes, waits for confirmation |

## Parallel vs Sequential

**Run in parallel** when tasks are independent:

```
User: "Add a delete_customer tool"

Parallel:
  → planner    (identify affected files + steps)
  → tdd-guide  (write failing test)

Then sequential:
  → implement (handler + tool method)
  → code-reviewer
  → /test-coverage
```

**Run sequentially** when output of one feeds the next:

```
build-error-resolver → (fix applied) → code-reviewer → /test-coverage
```

## Escalation Rules

- `build-error-resolver` gets 3 attempts max before escalating to user
- `planner` must reach 97% confidence before producing a plan
- Any agent that encounters an unexpected state (unknown files, missing deps) should STOP and report — never guess
