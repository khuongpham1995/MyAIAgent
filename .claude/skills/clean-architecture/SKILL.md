---
name: clean-architecture
description: >
  Clean Architecture patterns for the MyAIAgent solution. Covers layer
  responsibilities, dependency direction, DI registration, shared types,
  and how to add new features across all four layers for any of the three
  services (McpServer, Orchestrator, KernelMemory).
  Load when: adding a new feature, refactoring layers, debugging a dependency
  issue, asked about project structure, or adding a new service module.
---

# Clean Architecture (MyAIAgent)

## Overview

The solution has three backend services, each with four layers. `MyAIAgent.Common` is also structured as Clean Architecture layers — a shared base that every service extends.

```
Common.Domain ← Common.Application ← Common.Infrastructure
     ↑                  ↑                      ↑
[Svc].Domain      [Svc].Application      [Svc].Infrastructure ← [Svc].Presentation

(McpServer / Orchestrator / KernelMemory each follow the same pattern)
```

**Dependency direction:** Domain ← Application ← Infrastructure ← Presentation. Never reverse.

### Common Layer Reference Rules

| Service layer | References Common layer |
|---|---|
| `[Service].Domain` | `Common.Domain` |
| `[Service].Application` | `Common.Application` (gets `Common.Domain` transitively) |
| `[Service].Infrastructure` | `Common.Infrastructure` (gets `Common.Domain` transitively) |

## Layer Responsibilities

### Domain

The innermost layer — no project or package references except `MyAIAgent.Common` (for `ResponseModel<T>`).

- **Entities** — plain C# records/classes representing domain objects
- **Interfaces** — contracts for infrastructure implementations (e.g. `IApiClient<T>`, `IDocumentService`)
- **Models** — domain value objects and result types
- **Constants** — tool names, config keys, error messages

```csharp
// Domain defines the contract; Infrastructure implements it
public interface IDocumentService
{
    Task<DocumentQueryResult> AskAsync(string question, CancellationToken ct);
    Task<DocumentUploadResult> UploadDocumentAsync(string filePath, string documentId, CancellationToken ct);
    Task<bool> IsDocumentReadyAsync(string documentId, CancellationToken ct);
}
```

### Application

Business logic only. References Domain + Common.

- **Commands/Queries** — `IRequest<ResponseModel<T>>` sealed records
- **Handlers** — `IRequestHandler<TRequest, TResponse>` implementations
- **Validators** — `AbstractValidator<T>` (FluentValidation), co-located with the handler
- **Behaviors** — `ValidationBehavior<TRequest, TResponse>` (MediatR pipeline)
- **Extensions** — `IoCExtension.AddApplication()` registers MediatR + FluentValidation + Scrutor scan

```csharp
namespace MyAIAgent.KernelMemory.Application.Handlers;

public sealed record AskDocumentQuestionQuery(string Question)
    : IRequest<ResponseModel<DocumentQueryResult>>;

public sealed class AskDocumentQuestionHandler(IDocumentService documentService)
    : IRequestHandler<AskDocumentQuestionQuery, ResponseModel<DocumentQueryResult>>
{
    public async Task<ResponseModel<DocumentQueryResult>> Handle(
        AskDocumentQuestionQuery query, CancellationToken ct)
    {
        var result = await documentService.AskAsync(query.Question, ct);
        return ResponseModel<DocumentQueryResult>.Succeed(result);
    }
}

public sealed class AskDocumentQuestionValidator : AbstractValidator<AskDocumentQuestionQuery>
{
    public AskDocumentQuestionValidator()
    {
        RuleFor(x => x.Question).NotEmpty().MaximumLength(2000);
    }
}
```

### Infrastructure

Adapts external systems to domain interfaces. References Application + Domain + Common.

- **Services** — implement domain interfaces (e.g. `DocumentService`, `ChatOrchestratorService`)
- **ApiClients** — `ApiClient<T>` implements `IApiClient<T>` via `IHttpClientFactory`
- **Extensions** — `IoCExtension.AddInfrastructure(IConfiguration)` registers HTTP clients, SDKs, Scrutor scan
- **Extensions** — `RetryExtension` wires Polly retry policies

```csharp
public sealed class DocumentService(IKernelMemory kernelMemory)
    : IDocumentService, IScoped
{
    public async Task<DocumentQueryResult> AskAsync(string question, CancellationToken ct)
    {
        var answer = await kernelMemory.AskAsync(question, cancellationToken: ct);
        return new DocumentQueryResult(answer.Result);
    }
}
```

### Presentation

Composition root and HTTP surface. References Application + Infrastructure + Domain.

- **Program.cs** — DI wiring (`AddInfrastructure` + `AddApplication`), middleware, endpoint mapping
- **McpTools/** (McpServer only) — `[McpServerTool]` methods, dispatch via `ISender`
- **Controllers/** or minimal API routes (Orchestrator) — map HTTP requests to MediatR commands

## Common Layer Contents

### `MyAIAgent.Common.Domain` (no dependencies)

| Type | Namespace | Purpose |
|---|---|---|
| `ResponseModel<T>` | `MyAIAgent.Common.Domain.Models` | Standard result wrapper for all handlers |
| `ITransient` | `MyAIAgent.Common.Domain.ServiceLifetime` | DI: transient lifetime marker |
| `IScoped` | `MyAIAgent.Common.Domain.ServiceLifetime` | DI: scoped lifetime marker |
| `ISingleton` | `MyAIAgent.Common.Domain.ServiceLifetime` | DI: singleton lifetime marker |

### `MyAIAgent.Common.Application` (references Common.Domain + MediatR + FluentValidation)

| Type | Purpose |
|---|---|
| `ValidationBehavior<TRequest, TResponse>` | Shared MediatR pipeline validation behavior — used by all service Application layers |

### `MyAIAgent.Common.Infrastructure` (references Common.Domain + Scrutor + Polly)

| Type | Purpose |
|---|---|
| `ServiceCollectionExtension` | Scrutor-based DI scan helpers (`AddTransientAsMatchingInterfaces`, etc.) |
| `RetryExtension` | Polly HTTP retry policy wiring (exponential backoff, jitter) |

`ResponseModel<T>` is defined **once** in `Common.Domain`. `ValidationBehavior` is defined **once** in `Common.Application`. Do not duplicate either in service projects.

## DI Registration Pattern

Each service has two `IoCExtension` static classes:

**Application layer:**
```csharp
public static class IoCExtension
{
    public static void AddApplication(this IServiceCollection services)
    {
        services
            .AddTransientAsMatchingInterfaces(Assembly.GetExecutingAssembly())
            .AddScopedAsMatchingInterfaces(Assembly.GetExecutingAssembly())
            .AddSingletonAsMatchingInterfaces(Assembly.GetExecutingAssembly());

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()))
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))
            .AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);
    }
}
```

**Infrastructure layer:**
```csharp
public static class IoCExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddTransientAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddScopedAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddSingletonAsMatchingInterfaces(typeof(IoCExtension).Assembly);

        // Register SDK clients, HttpClients, etc. manually:
        services.AddSingleton<IKernelMemory>(_ =>
            new MemoryWebClient(configuration["KernelMemory:BaseUrl"]!));
    }
}
```

Manual registrations are only needed for:
- External SDK clients that don't implement marker interfaces
- `HttpClient` via `AddHttpClient<T, TImpl>()`
- MediatR + FluentValidation (registered in Application `IoCExtension`)

## Adding a Feature — Step by Step

For any service (McpServer, Orchestrator, KernelMemory):

```
1. Domain/Constants/    → add constants if needed (tool names, config keys)
2. Domain/Entities/     → add entity record if new data shape required
3. Domain/Interfaces/   → add interface if new external dependency introduced
4. Application/Handlers/→ add IRequest<ResponseModel<T>> record + IRequestHandler + AbstractValidator
5. Infrastructure/      → implement new interface from step 3 (if added)
6. Presentation/        → add HTTP endpoint or [McpServerTool] method
7. Test/                → add unit tests mirroring Application + Infrastructure structure
```

## Cross-Service Rules

- Services communicate via **HTTP only** — never via project references
- `MyAIAgent.Common` is the only shared project reference across service boundaries
- The MCP Server is the tool surface; the Orchestrator is the consumer
- The KernelMemory service exposes the standard KernelMemory HTTP API

## Anti-patterns

```csharp
// DON'T — reference a sibling service's project directly
<ProjectReference Include="..\MyAIAgent.Orchestrator.Domain\..." />  // wrong

// DON'T — call Infrastructure directly from Presentation
app.MapPost("/chat", async (IChatClient client) => ...);  // bypass MediatR

// DON'T — put ResponseModel<T> in a service Domain project
// It belongs in MyAIAgent.Common.Models — defined once, used everywhere

// DON'T — reverse the dependency direction
public class MyHandler(IServiceFromPresentationLayer svc) { }  // wrong
```
