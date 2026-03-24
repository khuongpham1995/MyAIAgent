---
description: >
  Run build, tests, anti-pattern scan, and format check for MyAIAgent.
  Use before every PR or after completing a feature.
  Trigger: "test coverage", "verify", "check everything", "is this ready", "pre-PR check".
---

# /test-coverage — Build & Test Pipeline

## What

Runs 4 sequential phases to confirm the codebase is in a releasable state.
Short-circuits on critical failures (phases 1–2).

| Phase | Command | What it catches |
|---|---|---|
| 1. Build | `dotnet build` | Compilation errors, missing references |
| 2. Tests | `dotnet test` | Regressions, failing unit tests |
| 3. Anti-patterns | Grep modified `.cs` files | DateTime.Now, new HttpClient(), async void, .Result |
| 4. Format | `dotnet format --verify-no-changes` | Style drift |

## How

### Phase 1 — Build (critical, short-circuits on failure)

```bash
dotnet build src/MyAIAgent.sln --no-restore --verbosity quiet
```

If build fails: STOP. Report errors. Do not proceed.

### Phase 2 — Tests (critical, short-circuits on failure)

```bash
dotnet test src/MyAIAgent.sln --no-build --verbosity quiet
```

Any failing test = FAIL. Report test name and failure message.

### Phase 3 — Anti-pattern Scan

Scan all modified `.cs` files for:

- `DateTime\.Now` or `DateTime\.UtcNow` → use `TimeProvider`
- `new HttpClient\(\)` → use `IApiClient<T>`
- `async void` (non-event-handler) → return `Task`
- `\.Result\b` or `\.GetAwaiter\(\)\.GetResult\(\)` → await instead
- String interpolation in log templates → use structured parameters

### Phase 4 — Format Check

```bash
dotnet format src/MyAIAgent.sln --verify-no-changes --verbosity quiet
```

Reports files with formatting drift. Does NOT auto-fix.

## Output

```
## Pipeline Results

| Phase | Result | Details |
|---|---|---|
| 1. Build | PASS | 0 errors |
| 2. Tests | PASS | 12 passed, 0 failed |
| 3. Anti-patterns | WARN | 1 finding |
| 4. Format | PASS | Clean |

Verdict: READY FOR REVIEW
```

Verdicts:
- **READY FOR REVIEW** — all PASS or only non-blocking WARNs
- **NEEDS FIXES** — any phase FAIL, with specific steps to fix
