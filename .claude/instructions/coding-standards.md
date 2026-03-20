---
applyTo: '**'
---

# Coding Standards — Quick Reference

Full rules are in `rules/`. This is the always-loaded summary.

## Must-Follow

- Primary constructors for DI — no manual field assignments
- Records for DTOs, commands, queries, response models
- `sealed` on all classes not designed for inheritance
- File-scoped namespaces — always
- `async`/`await` everywhere — no `.Result`, `.Wait()`
- Propagate `CancellationToken` through every async chain
- No `new HttpClient()` — use `IApiClient<T>`
- No `DateTime.UtcNow` — inject `TimeProvider`
- No regions, no "what" comments

## Anti-patterns — Never Do

| ❌ | ✅ |
|---|---|
| `new HttpClient()` | `IApiClient<T>` |
| `DateTime.Now` / `DateTime.UtcNow` | `TimeProvider` |
| `async void` | `async Task` |
| `.Result` or `.Wait()` | `await` |
| Manual DI for marker types | `ITransient` / `IScoped` / `ISingleton` |
| Hardcoded secrets | Configuration + user secrets |
| `catch (Exception)` | Specific exception types |
| String interpolation in log templates | Structured parameters |
