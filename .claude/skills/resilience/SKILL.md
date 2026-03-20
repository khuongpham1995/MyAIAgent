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

All HTTP calls go through `IApiClient<T>`. Never inject `HttpClient` directly into a handler or service.

```csharp
// Domain interface
public interface IApiClient<T>
{
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct);
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<T>> GetByNameAsync(string name, CancellationToken ct);
    Task<T> PostAsync(T entity, CancellationToken ct);
    Task<T> PutAsync(Guid id, T entity, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
```

## Infrastructure Implementation

`ApiClient<T>` implements `IApiClient<T>` using `HttpClient` (injected via factory):

```csharp
public sealed class ApiClient<T>(HttpClient httpClient, ILogger<ApiClient<T>> logger)
    : IApiClient<T>, IScoped
{
    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct)
    {
        var response = await httpClient.GetFromJsonAsync<IReadOnlyList<T>>("", ct);
        return response ?? [];
    }

    public async Task<T> PostAsync(T entity, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync("", entity, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(ct))!;
    }

    // ... other methods
}
```

## Retry Policy — RetryExtension

`RetryExtension` wires a Polly resilience pipeline onto every named `HttpClient`:

```csharp
// Infrastructure/Extensions/RetryExtension.cs
public static IHttpClientBuilder AddRetryPolicy(this IHttpClientBuilder builder)
{
    return builder.AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromMilliseconds(500);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
    });
}
```

## Registering a New HTTP Client

In `Infrastructure/Extensions/IoCExtension.cs`:

```csharp
services
    .AddHttpClient<IApiClient<Customer>, ApiClient<Customer>>(client =>
    {
        client.BaseAddress = new Uri(configuration["MockApi:BaseUrl"]!);
    })
    .AddRetryPolicy();
```

**Rules:**
- Always use `AddHttpClient<TInterface, TImpl>()` — never `new HttpClient()`
- Always call `.AddRetryPolicy()` — every registered client gets retry + exponential backoff
- Base URL comes from configuration, never hardcoded
- Register one `HttpClient` per entity type (Customer, etc.)

## CancellationToken Propagation

Every async method in the call chain must accept and forward `CancellationToken`. Never drop it.

```csharp
// DO — token propagated all the way down
public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct)
    => await httpClient.GetFromJsonAsync<IReadOnlyList<Customer>>("", ct) ?? [];

// DON'T — token silently dropped
public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct)
    => await httpClient.GetFromJsonAsync<IReadOnlyList<Customer>>("") ?? [];
```

## Anti-patterns

```csharp
// DON'T — direct HttpClient injection
public sealed class GetCustomersHandler(HttpClient client) { }

// DON'T — new HttpClient() anywhere
var client = new HttpClient { BaseAddress = new Uri("...") };

// DON'T — no retry policy
services.AddHttpClient<IApiClient<Customer>, ApiClient<Customer>>(...);
// missing: .AddRetryPolicy()

// DON'T — .Result or .Wait() blocking
var customers = client.GetAllAsync(ct).Result;
```
