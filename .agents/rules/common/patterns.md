---
alwaysApply: true
description: >
  Common patterns for MyAIAgent: Result pattern for expected failures,
  validation at the application boundary, and exception handling rules.
---

# Patterns

## Result Pattern for Expected Failures

Use `ResponseModel<T>` for expected failure paths: not found, validation failure, external API error. Never throw exceptions for predictable outcomes — exceptions are expensive and hide control flow.

Return `ResponseModel<T>.Succeed(value)` on success and `ResponseModel<T>.Fail(message)` on expected failure.

## Validation at the Application Boundary

`ValidationBehavior<TRequest, TResponse>` runs before every handler via the MediatR pipeline. Add an `AbstractValidator<TCommand>` for every command that accepts user input. Never defensively validate inside private methods or infrastructure — trust the pipeline.

## Exception Handling

| Scenario | Approach |
|---|---|
| User input invalid | FluentValidation via pipeline behavior — throws `ValidationException` |
| Entity not found | `ResponseModel<T>.Fail("not found")` |
| External API error | Catch `HttpRequestException`, return `ResponseModel<T>.Fail` with safe message |
| Unexpected crash | Let propagate to `GlobalExceptionHandler` |
| Cancellation | Propagate `CancellationToken`, let `OperationCanceledException` propagate |

- Never catch bare `Exception` inside handlers or services
- Catch specific exceptions at infrastructure boundaries and convert to `ResponseModel.Fail`
- Never `catch { /* ignore */ }` or catch and rethrow without adding context
- Never leak internal error details (URLs, IPs, stack traces) to callers — log them, return a safe message
