# Task Routing

Decision guide for routing tasks to the right command, agent, or skill.

## By Task Type

| Task | Route |
|---|---|
| New feature touching 3+ files | `/plan` → `/tdd` → implement → `/code-review` → `/test-coverage` |
| New MCP tool | `/plan` + load `mcp-server` skill + `mediatr-cqrs` skill |
| New handler only (no tool) | `/tdd` + load `mediatr-cqrs` skill |
| Build or test failure | `/build-fix` (build-error-resolver agent) |
| Code smells / duplication | `refactor-cleaner` agent |
| External HTTP integration | load `resilience` skill + `security-reviewer` agent |
| RAG / document feature | load `kernel-memory` skill |
| Before PR | `/test-coverage` |

## Confidence Gate

Before starting any implementation, Claude must be ≥ 97% confident in what to build.
If confidence is lower, ask follow-up questions. See `instructions/follow-up-questions.md`.

## Complexity Thresholds

| Complexity | Action |
|---|---|
| 1–2 files, clear scope | Implement directly |
| 3+ files or new module | `/plan` first, wait for confirmation |
| Architecture change | `/plan` + `clean-architecture` skill + user sign-off |
| Security-sensitive (auth, HTTP, input) | `security-reviewer` before merging |
