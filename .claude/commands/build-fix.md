---
description: >
  Autonomously fix dotnet build errors and test failures in MyAIAgent.
  Delegates to build-error-resolver agent. Tries up to 3 times before
  escalating to the user.
  Trigger: "fix build", "build errors", "tests failing", "won't compile".
---

# /build-fix — Autonomous Build Error Resolution

## What

Invokes the `build-error-resolver` agent to diagnose and fix .NET build or test failures without hand-holding.

## When

- `dotnet build` exits non-zero
- `dotnet test` shows failures
- Type errors, missing references, ambiguous calls

## How

### Step 1 — Run and Capture

```bash
dotnet build MyAIAgent.sln --no-restore --verbosity normal 2>&1
```

or

```bash
dotnet test MyAIAgent.McpServer.Test/MyAIAgent.McpServer.Test.csproj --verbosity normal 2>&1
```

### Step 2 — Categorize

Load `.claude/agents/build-error-resolver.md`. Identify:
- CS code (missing using, type mismatch, interface mismatch, nullable)
- Test failure type (assertion, mock not matched, exception)

### Step 3 — Fix

Apply minimal, targeted fix to the failing file only. Do not refactor surrounding code.

### Step 4 — Verify

```bash
dotnet build MyAIAgent.sln --no-restore --verbosity quiet
dotnet test MyAIAgent.McpServer.Test/MyAIAgent.McpServer.Test.csproj --no-build --verbosity quiet
```

### Step 5 — Retry or Escalate

- If still failing: retry with a different approach (max 3 attempts)
- After 3 failures: report to user with error, what was tried, and what's needed

## Related

- `/verify` — full verification after build is fixed
- `/code-review` — review the fix for correctness
