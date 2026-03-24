---
name: resilience
description: >
  HTTP resilience patterns for MyAIAgent. Covers IApiClient<T>, the Polly
  retry policy via RetryExtension, IHttpClientFactory registration, and
  CancellationToken propagation.
  Load when: adding HTTP clients, configuring retry policies, asked about
  resilience, IApiClient, Polly, or HttpClient registration.
---

# HTTP Resilience (MyAIAgent)

## IApiClient<T> — The Only HTTP Abstraction

All HTTP calls go through `IApiClient<T>` (defined in `McpServer.Domain`). Never inject `HttpClient` directly into a handler or service.

`ApiClient<T>` (in `McpServer.Infrastructure/ApiClients/`) implements the interface using `IHttpClientFactory`. It is registered as `IScoped`.

## Retry Policy

`RetryExtension` (in `Common.Infrastructure/Extensions/`) adds a Polly standard resilience pipeline:
- 3 retry attempts, exponential backoff, 500ms base delay

Every registered `HttpClient` must call `.AddRetryPolicy()` — no exceptions.

## Registering a New HTTP Client

In `Infrastructure/Extensions/IoCExtension.cs`, call `AddHttpClient<IApiClient<T>, ApiClient<T>>()` with the base URL from configuration, then chain `.AddRetryPolicy()`.

- One `HttpClient` per entity type
- Base URL always from configuration — never hardcoded
- Always call `.AddRetryPolicy()`

## CancellationToken

Every async method must accept and forward `CancellationToken`. See `.claude/rules/common/performance.md` for the full async rules.
