---
alwaysApply: true
description: >
  Common patterns for MyAIAgent: Result pattern for expected failures,
  validation at the application boundary, and exception handling rules.
---

# Patterns

## Result Pattern for Expected Failures

- **DO** use `Result<T>` / `ResponseModel<T>` for expected failure paths: not found, validation failure, external API error
- **DON'T** throw exceptions for predictable outcomes — exceptions are expensive and hide control flow

```csharp
// DO — return Result for expected failures
public async Task<Result<Customer>> Handle(GetCustomerCommand command, CancellationToken ct)
{
    var customer = await _client.GetAsync(command.Id, ct);
    return customer is not null
        ? Result.Success(customer)
        : Result.Failure<Customer>($"Customer {command.Id} not found");
}

// DON'T — exception for flow control
if (customer is null)
    throw new NotFoundException($"Customer {command.Id} not found");
```

## Validation at the Application Boundary

- FluentValidation runs via `ValidationBehavior<TRequest, TResponse>` in the MediatR pipeline
- Add an `AbstractValidator<TCommand>` for every command that accepts user input
- Don't defensively validate inside private methods or infrastructure — trust the pipeline

## Exception Handling

- **DON'T** catch bare `Exception` inside handlers or services
- **DO** catch specific exceptions at infrastructure boundaries (HTTP calls, file I/O) and convert to `Result.Failure`
- Let unhandled exceptions propagate — the host's global exception handler catches them
- **DON'T** `catch { /* ignore */ }` or catch and rethrow without adding context

| Scenario | Approach |
|---|---|
| User input invalid | FluentValidation via pipeline behavior |
| Entity not found | `Result.Failure<T>("not found")` |
| External API error | Catch `HttpRequestException`, return `Result.Failure` |
| Unexpected crash | Let propagate to global handler |
| Cancellation | Propagate `CancellationToken`, let `OperationCanceledException` propagate |
