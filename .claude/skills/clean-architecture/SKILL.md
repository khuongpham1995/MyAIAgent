---
name: clean-architecture
description: >
  Clean Architecture patterns for the MyAIAgent solution. Covers layer
  responsibilities, dependency direction, DI registration, and how to add
  new features across all four layers.
  Load when: adding a new feature, refactoring layers, debugging a dependency
  issue, or asked about project structure.
---

# Clean Architecture (MyAIAgent)

## Layer Responsibilities

### Domain — `MyAIAgent.McpServer.Domain`

The innermost layer. No project references to any other layer.

- **Entities** — plain C# classes: `Customer`
- **Interfaces** — contracts for infrastructure: `IApiClient<T>`, `IKernelMemoryService`
- **Models** — `ResponseModel<T>` (standard response wrapper)
- **Constants** — `McpServerConstant` (MCP tool names, descriptions)

```csharp
// Correct — Domain defines the interface, Infrastructure implements it
namespace MyAIAgent.McpServer.Domain.Interfaces;

public interface IKernelMemoryService
{
    Task<string> AskAsync(string question, string documentId, CancellationToken ct);
    Task UploadDocumentAsync(string documentId, Stream content, CancellationToken ct);
    Task<bool> IsDocumentReadyAsync(string documentId, CancellationToken ct);
}
```

### Application — `MyAIAgent.McpServer.Application`

Business logic only. References Domain, not Infrastructure.

- **Commands/Queries** — `IRequest<T>` records
- **Handlers** — `IRequestHandler<TRequest, TResponse>` implementations
- **Validators** — `AbstractValidator<T>` (FluentValidation)
- **Behaviors** — `ValidationBehavior<TRequest, TResponse>` (MediatR pipeline)
- **Extensions** — `IoCExtension` registers MediatR + FluentValidation

```csharp
namespace MyAIAgent.McpServer.Application.Handlers.Customer;

public sealed record CreateCustomerCommand(
    string Name, string Address, string Phone, string Email
) : IRequest<ResponseModel<CustomerDto>>;

public sealed class CreateCustomerHandler(IApiClient<Customer> client)
    : IRequestHandler<CreateCustomerCommand, ResponseModel<CustomerDto>>
{
    public async Task<ResponseModel<CustomerDto>> Handle(
        CreateCustomerCommand command, CancellationToken ct)
    {
        var customer = new Customer
        {
            Name = command.Name,
            Address = command.Address,
            Phone = command.Phone,
            Email = command.Email
        };
        var created = await client.PostAsync(customer, ct);
        return ResponseModel<CustomerDto>.Success(created.ToDto());
    }
}
```

### Infrastructure — `MyAIAgent.McpServer.Infrastructure`

Implements Domain interfaces. References Application + Domain.

- **ApiClients** — `ApiClient<T>` implements `IApiClient<T>`
- **Services** — `KernelMemoryService` implements `IKernelMemoryService`
- **Extensions** — `IoCExtension` registers HttpClient, KernelMemory client
- **Extensions** — `RetryExtension` wires Polly retry policy

### Presentation — `MyAIAgent.McpServer.Presentation`

Composition root + MCP tool surface. References Application + Infrastructure.

- **Program.cs** — DI wiring, MCP server setup
- **McpTools/** — `[McpServerTool]` methods; thin, dispatch via `ISender`

## Dependency Injection Flow

```
Scrutor scans assemblies for ITransient / IScoped / ISingleton
          ↓
ServiceCollectionExtension.AddCommonServices(assembly)
          ↓
Types are registered with their matching lifetime automatically
```

Manual registrations are only needed for:
- External clients (HttpClient via `AddHttpClient<T>`)
- Third-party SDKs that don't implement marker interfaces (KernelMemory)
- MediatR and FluentValidation (registered in `Application/Extensions/IoCExtension.cs`)

## Adding a Feature — Step by Step

```
1. McpServerConstant.cs      → add ToolName and Description constants
2. Domain/Entities/          → add entity if new data shape needed
3. Domain/Interfaces/        → add interface if new external dependency
4. Application/Handlers/     → add IRequest<T> record + IRequestHandler implementation
5. Application/Validators/   → add AbstractValidator<T> if command has user input
6. Infrastructure/           → implement new interface if added in step 3
7. Presentation/McpTools/    → add [McpServerTool] method dispatching to ISender
8. McpServer.Test/           → add unit tests mirroring Application + Infrastructure layers
```
