---
alwaysApply: true
description: >
  C# 14 / .NET 10 coding conventions, Clean Architecture layer boundaries,
  and DI marker interface pattern for the MyAIAgent solution.
---

# .NET Coding Style

## File Organization

- **File-scoped namespaces always** — block-scoped namespaces waste indentation
- **One type per file** — file name must match the type name exactly
- **Member order:** constants → fields → constructors → properties → public methods → private methods

## Type Declarations

- **Primary constructors for DI** — no manual `_field = field` ceremony

```csharp
// DO
public sealed class KernelMemoryService(IKernelMemoryClient client, ILogger<KernelMemoryService> logger) : IKernelMemoryService { }

// DON'T
public class KernelMemoryService : IKernelMemoryService
{
    private readonly IKernelMemoryClient _client;
    public KernelMemoryService(IKernelMemoryClient client) { _client = client; }
}
```

- **Records for DTOs, commands, queries, and response models**

```csharp
public sealed record CreateCustomerCommand(string Name, string Address, string Phone, string Email) : IRequest<Result<Customer>>;
public sealed record CustomerResponse(Guid Id, string Name, string Email);
```

- **`sealed` on all classes not designed for inheritance** — communicates intent and enables JIT devirtualization
- **`internal` by default** — `public` only when consumed across project boundaries

## Expressions and Patterns

- **Collection expressions** over constructor calls

```csharp
// DO
List<string> names = ["Alice", "Bob"];
// DON'T
var names = new List<string> { "Alice", "Bob" };
```

- **Pattern matching** over if-else chains

```csharp
var message = result switch
{
    { IsSuccess: true, Value: var customer } => $"Created {customer.Name}",
    { Errors: var errors } => string.Join(", ", errors)
};
```

## Naming

- **`var`** for obvious types; explicit types when the type isn't clear from context
- **PascalCase** for public members, types, namespaces, methods
- **camelCase** for local variables and parameters
- **No `_` prefix** on private fields when using primary constructors
- **No regions. Ever.**

## Architecture — Layer Boundaries

Dependency direction is inward: **Domain ← Application ← Infrastructure ← Presentation**. Never reverse.

| Layer | Project | Allowed dependencies |
|---|---|---|
| Domain | `MyAIAgent.McpServer.Domain` | None |
| Application | `MyAIAgent.McpServer.Application` | Domain |
| Infrastructure | `MyAIAgent.McpServer.Infrastructure` | Application + Domain |
| Presentation | `MyAIAgent.McpServer.Presentation` | Application + Infrastructure |
| Shared | `MyAIAgent.Common` | None |

## Dependency Injection — Marker Interface Pattern

`MyAIAgent.Common` provides three marker interfaces. Implement the right one; Scrutor registers it automatically.

```csharp
// DO — Scrutor picks this up automatically
public sealed class KernelMemoryService(MemoryWebClient client) : IKernelMemoryService, IScoped { }

// DON'T — manual registration when marker interface exists
services.AddScoped<IKernelMemoryService, KernelMemoryService>();
```

| Interface | Lifetime |
|---|---|
| `ITransient` | Per-resolution |
| `IScoped` | Per HTTP request |
| `ISingleton` | Application lifetime |

## MCP Tool Registration

- Tool name/description constants belong in `McpServerConstant.cs` (Domain layer)
- MCP tool classes live in `Presentation/McpTools/` with `[McpServerTool]`
- Tools dispatch via MediatR — never call infrastructure directly from a tool method
