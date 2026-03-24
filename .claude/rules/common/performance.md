---
alwaysApply: true
description: >
  Performance rules for MyAIAgent: async patterns, HTTP client management,
  CancellationToken propagation, and time abstraction.
---

# Performance Rules

## Async

- Always `await` — never `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`
- `async void` is forbidden — always return `Task`
- Propagate `CancellationToken` through every async call chain — never drop it silently

## HTTP Clients

- Never instantiate `new HttpClient()` — use `IApiClient<T>` via `IHttpClientFactory`
- Always configure a timeout — the default 100s is too long for most service-to-service calls

## Time

- Inject `TimeProvider` instead of calling `DateTime.UtcNow` directly — enables testable time logic

## Anti-patterns — Quick Reference

| Anti-pattern | Replacement |
|---|---|
| `.Result` / `.Wait()` | `await` |
| `async void` | `async Task` |
| `new HttpClient()` | `IApiClient<T>` |
| `DateTime.Now` / `DateTime.UtcNow` | `TimeProvider.GetUtcNow()` |
| Dropped `CancellationToken` | Propagate to all `async` calls |
