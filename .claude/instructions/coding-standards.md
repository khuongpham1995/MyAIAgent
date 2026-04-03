---
applyTo: '**'
---

# Coding Standards

Full rules: `rules/dotnet/coding-style.md`, `rules/dotnet/di.md`, `rules/common/performance.md`, `rules/common/patterns.md`.

## Critical Rules (Always Apply)

- Primary constructors for DI — no manual field assignments
- Records for DTOs, commands, queries, response models
- `sealed` on all classes not designed for inheritance
- File-scoped namespaces — always
- `async`/`await` everywhere — never `.Result`, `.Wait()`
- Propagate `CancellationToken` through every async chain
- No `new HttpClient()` — use `IApiClient<T>`
- No `DateTime.UtcNow` — inject `TimeProvider`
- No regions, no "what" comments
