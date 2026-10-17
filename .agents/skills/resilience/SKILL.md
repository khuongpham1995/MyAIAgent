---
name: resilience
description: HTTP client registration, Polly retry behavior and cancellation in MyAIAgent. Use when adding or changing external HTTP clients or resilience policies.
---

# HTTP resilience (MyAIAgent)

Use typed clients registered with `AddHttpClient<TInterface, TImplementation>` in
`src/MyAIAgent.<Service>.Infrastructure/IoCExtension.cs`. Infrastructure
implementations receive `HttpClient`; handlers depend on the service's interfaces.
For concrete examples inspect `ISearchProvider`/`GoogleSearchClient`,
`IPageFetcher`/`PageFetcher`, and MainSubSys clients. Do not invent a generic
`IApiClient` abstraction absent from the current source.

The shared extension is `AddHttpRetryPolicy()` in
`src/MyAIAgent.Common.Infrastructure/Extensions/RetryExtension.cs`. Its current
retry options are three attempts, a one-second base delay, exponential backoff and
jitter. Check source before changing or describing these settings.

Set appropriate timeouts and configuration-based service URLs. Before applying
retries to writes/uploads/tool calls, assess replay safety and idempotency; do not
retry side effects blindly. Preserve authentication boundaries and disposal of
responses/streams. Forward cancellation through every external call.

See `.agents/rules/common/performance.md` for async conventions.
