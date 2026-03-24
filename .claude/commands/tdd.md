---
description: >
  Test-driven development workflow for MyAIAgent. Writes the failing xUnit test
  first, implements minimal code to pass, then refactors.
  Trigger: "tdd", "write tests first", "test-driven", "red-green-refactor".
---

# /tdd — Test-Driven Development

## What

Drives the implementation of a new handler, validator, or infrastructure service via RED → GREEN → REFACTOR.

Delegates to the `tdd-guide` agent for test writing.

## Workflow

### Phase 1: RED — Write Failing Test

Load `.claude/agents/tdd-guide.md`. Write the test file in the appropriate service test project before any production code exists.

Determine the test project path from the service being worked on (e.g., `src/MyAIAgent.McpServer.Test/`, `src/MyAIAgent.Orchestrator.Test/`).

```bash
dotnet test src/[Service].Test/[Service].Test.csproj \
  --filter "FullyQualifiedName~[TestClass]" \
  --verbosity quiet
```

Expected: test fails. If it passes, the test is wrong — fix it first.

### Phase 2: GREEN — Minimal Implementation

Write the smallest production code that makes the test pass. No extra logic, no pre-emptive abstractions.

```bash
dotnet test src/[Service].Test/[Service].Test.csproj \
  --filter "FullyQualifiedName~[TestClass]" \
  --verbosity quiet
```

All tests must pass.

### Phase 3: REFACTOR

Improve the code — extract, rename, simplify — without changing behaviour. Tests must stay green.

### Phase 4: Full Suite

```bash
dotnet test src/MyAIAgent.sln --verbosity quiet
```

No regressions across all services.

## Test Naming Convention

`FunctionName_When_Should`

Examples: `Handle_WhenCommandIsValid_ShouldReturnSuccess`, `Handle_WhenNameIsNull_ShouldThrowValidationException`, `ValidateAsync_WhenEmailIsEmpty_ShouldReturnValidationError`

## Coverage Targets

- Happy path: **required**
- Each failure path: **required**
- Each validation rule: **required**

