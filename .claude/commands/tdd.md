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

Load `.claude/agents/tdd-guide.md`. Write the test file in `MyAIAgent.McpServer.Test/` before any production code exists.

```bash
# Confirm the test fails (compilation or assertion)
dotnet test MyAIAgent.McpServer.Test/MyAIAgent.McpServer.Test.csproj \
  --filter "FullyQualifiedName~[TestClass]" \
  --verbosity quiet
```

Expected: test fails. If it passes, the test is wrong — fix it first.

### Phase 2: GREEN — Minimal Implementation

Write the smallest production code that makes the test pass. No extra logic, no pre-emptive abstractions.

```bash
dotnet test MyAIAgent.McpServer.Test/MyAIAgent.McpServer.Test.csproj \
  --filter "FullyQualifiedName~[TestClass]" \
  --verbosity quiet
```

All tests must pass.

### Phase 3: REFACTOR

Improve the code — extract, rename, simplify — without changing behaviour. Tests must stay green.

### Phase 4: Full Suite

```bash
dotnet test MyAIAgent.McpServer.Test/MyAIAgent.McpServer.Test.csproj --verbosity quiet
```

No regressions.

## Test Naming Convention

```
[MethodName]_[Scenario]_[ExpectedResult]

Handle_ValidCreateCustomerCommand_ReturnsSuccess
Handle_NullName_ThrowsValidationException
ValidateAsync_EmptyEmail_ReturnsEmailError
```

## Coverage Targets

- Happy path: **required**
- Each failure path: **required**
- Each validation rule: **required**

## Related

- `/add-handler` — scaffold the production-side skeleton after TDD
- `/add-tool` — scaffold the full tool including tests
- `/verify` — run full verification after all tests are green
