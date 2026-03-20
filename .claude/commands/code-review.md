---
description: >
  Invoke the code-reviewer agent on recent changes in MyAIAgent. Reviews
  Clean Architecture compliance, MCP patterns, naming, and security.
  Trigger: "review", "code review", "check my code", "review this change".
---

# /code-review — C# Code Review

## What

Invokes the `code-reviewer` agent to review staged or recent changes against the project's rules and skills.

## When

- After implementing a new handler or tool method
- After any refactoring
- Before running `/verify` or creating a PR
- When unsure if code follows conventions

## How

### Step 1 — Gather Changes

```bash
git diff --staged     # if reviewing staged changes
git diff HEAD~1       # if reviewing last commit
```

### Step 2 — Load Reviewer

Load `.claude/agents/code-reviewer.md` and apply its checklist (CRITICAL → HIGH → MEDIUM → LOW).

Cross-reference against:
- `.claude/rules/architecture.md` — layer boundaries, DI pattern, tool registration
- `.claude/rules/coding-style.md` — C# 14 conventions, naming, sealed
- `.claude/rules/error-handling.md` — Result pattern, validation boundary
- `.claude/rules/security.md` — secrets, SSL, logging, input validation

### Step 3 — Report Findings

```
## Code Review Results

### [CRITICAL] ...
### [HIGH] ...
### [MEDIUM] ...
### [LOW] ...

Verdict: APPROVE / NEEDS FIXES / WARN
```

### Step 4 — Act on Findings

- CRITICAL or HIGH findings → fix before proceeding
- MEDIUM findings → fix if quick, otherwise document
- LOW findings → note and proceed

## Related

- `/verify` — run build + tests + format after addressing findings
- `/plan` — if review reveals a deeper design issue
