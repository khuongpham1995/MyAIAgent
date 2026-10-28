---
alwaysApply: true
description: >
  Testing strategy, naming conventions, and mocking guidelines
  for MyAIAgent using xUnit, Moq, and FluentAssertions.
---

# Testing Rules

## Strategy

**Unit tests** — test a single class in isolation. Mock all external dependencies. Fast, no I/O.

**Integration tests** — test a slice of the system with real collaborators (e.g., real MediatR pipeline with a real validator). Mock only the outermost I/O boundary (HTTP client, database).

- Mock external boundaries only — never mock types you own (handlers, validators, mappers)
- Test the handler and validator, not the thin Presentation wrapper — Presentation dispatches only

## Structure

Mirror the source layout inside the test project. Each service has its own test project:

```
tests/Services/[Service]/MyAIAgent.[Service].Tests/
  Unit/
    Application/{Feature}/Commands/{RequestName}/ or Queries/{RequestName}/
                            → handler + validator unit tests
    Infrastructure/          → service + client unit tests
  Integration/
    Application/             → full MediatR pipeline tests (real behavior + validator)
```

## Naming

`FunctionName_When_Should`

Examples: `Handle_WhenCommandIsValid_ShouldReturnSuccess`, `Handle_WhenEntityNotFound_ShouldReturnFailure`, `ValidateAsync_WhenNameIsEmpty_ShouldReturnValidationError`

## Test Pattern

Arrange / Act / Assert separated by blank lines. One act per test. Keep assertions focused.

## FluentAssertions

- Prefer `.Should().Be()`, `.Should().BeTrue()`, `.Should().Contain()` over raw `Assert.*`
- Collections: `.Should().HaveCount()`, `.Should().ContainSingle()`, `.Should().BeEmpty()`
- Exceptions: `.Should().ThrowAsync<T>()`

## Moq Guidelines

- `It.IsAny<T>()` for arguments you don't care about; `It.Is<T>(x => ...)` for precise matching
- Call `mock.Verify(...)` when confirming a call was made is critical to the test outcome
- Avoid `MockBehavior.Strict` unless every call must be asserted
