---
name: mediatr-cqrs
description: >
  MediatR CQRS patterns for MyAIAgent. Covers command/query definitions,
  handler implementation, FluentValidation pipeline behavior, and how
  to wire everything through the MediatR pipeline.
  Load when: adding handlers, commands, queries, validators, or pipeline
  behaviors; or asked about how requests flow through the application layer.
---

# MediatR CQRS (MyAIAgent)

## Request Types

- **Command** — mutates state. `sealed record *Command([params]) : IRequest<ResponseModel<T>>`
- **Query** — read-only. `sealed record *Query([params]) : IRequest<ResponseModel<T>>`

Place each request in `Application/{Feature}/Commands/{RequestName}/` or
`Application/{Feature}/Queries/{RequestName}/`. Keep the request, handler, and
validator in separate files in that request folder, with namespaces matching
the path. Do not use a shared `Application/Handlers/` folder.

## Handler

`sealed class *Handler(IDependency dep) : IRequestHandler<TRequest, ResponseModel<T>>`

- One handler per command/query
- Primary constructor for DI
- Never call another handler directly — dispatch a separate command
- Return `ResponseModel<T>.Succeed(value)` or `ResponseModel<T>.Fail(message)`

## Validator

`sealed class *Validator : AbstractValidator<TRequest>` — place beside the request
and handler in the same Command/Query folder.

- Add a rule for every user-controlled input field
- Always use `.WithMessage()` — never rely on defaults
- No validator needed for commands with no user input

## Validation Pipeline

`ValidationBehavior<TRequest, TResponse>` (in `Common.Application`) runs before every handler. It collects all failures and throws `ValidationException`. The `GlobalExceptionHandler` converts it to `400 ResponseModel<ErrorResponseDto>`, with `Data.StatusCode` matching HTTP 400. Details remain in the envelope's `Message` and `Errors`.

**Do not** add `ResponseModel<T>` handling back into `ValidationBehavior` — it always throws.

## IoC Registration

`Application/IoCExtension.cs` registers MediatR, `ValidationBehavior`, and all validators via `AddValidatorsFromAssembly`. No manual per-handler registration needed.

## Response Model

Ordinary request handlers return `ResponseModel<T>` (defined once in `Common.Application/Models`).
Streaming keeps its existing event contract. Do not create service-level result wrappers.

## Mapper Convention

DTO → Command and entity → response mapping use Mapster `IRegister` classes in
`Application/Common/Mappings/`. Inject `MapsterMapper.IMapper` at the mapping site.
See `.agents/rules/dotnet/mapping.md`.
