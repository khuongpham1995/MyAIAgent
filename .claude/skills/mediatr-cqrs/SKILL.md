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

## Command and Query Definitions

Use records for immutable request types. Distinguish commands (write) from queries (read).

```csharp
// Command — mutates state
public sealed record CreateCustomerCommand(
    string Name,
    string Address,
    string Phone,
    string Email
) : IRequest<ResponseModel<Customer>>;

// Query — read-only
public sealed record GetCustomersQuery : IRequest<ResponseModel<IReadOnlyList<Customer>>>;
public sealed record GetCustomersByNameQuery(string Name) : IRequest<ResponseModel<IReadOnlyList<Customer>>>;
```

## Handler Implementation

```csharp
public sealed class GetCustomersHandler(IApiClient<Customer> client)
    : IRequestHandler<GetCustomersQuery, ResponseModel<IReadOnlyList<Customer>>>
{
    public async Task<ResponseModel<IReadOnlyList<Customer>>> Handle(
        GetCustomersQuery request, CancellationToken ct)
    {
        var customers = await client.GetAllAsync(ct);
        return ResponseModel<IReadOnlyList<Customer>>.Success(customers);
    }
}
```

**Rules:**
- Handlers are `sealed` — not designed for inheritance
- Handlers use primary constructors for DI
- One handler per command/query — no shared handlers for multiple requests
- Handlers never call other handlers directly — use separate commands if needed

## FluentValidation Pipeline Behavior

`ValidationBehavior<TRequest, TResponse>` intercepts every `IRequest` before the handler runs. It finds all `IValidator<TRequest>` implementations in the Application assembly and runs them.

```csharp
// The behavior (already implemented in Application/Behaviors/)
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

## Writing a Validator

```csharp
public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid address");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required");
    }
}
```

**Rules:**
- Validator class name matches the command/query: `CreateCustomerValidator` for `CreateCustomerCommand`
- Place validators next to or in the same file as their handler for locality
- `sealed` validators — not designed for inheritance
- Use `.WithMessage()` for every rule — don't rely on default messages

## IoC Registration

`IoCExtension.cs` (Application layer) registers everything:

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(IoCExtension).Assembly);
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    });

    services.AddValidatorsFromAssembly(typeof(IoCExtension).Assembly);

    return services;
}
```

## Response Model

All handlers return `ResponseModel<T>` (Domain layer):

```csharp
// Usage
return ResponseModel<Customer>.Success(customer);
return ResponseModel<Customer>.Failure("Customer not found");
```

## Anti-patterns

```csharp
// DON'T — non-record request type
public class CreateCustomerCommand : IRequest<ResponseModel<Customer>>
{
    public string Name { get; set; }  // mutable, not a record
}

// DON'T — handler calling another handler
public async Task<ResponseModel<Customer>> Handle(...)
{
    await _mediator.Send(new DeleteCustomerCommand(id), ct);  // use separate workflow
}

// DON'T — non-sealed handler
public class GetCustomersHandler : IRequestHandler<...>  // missing sealed
```
