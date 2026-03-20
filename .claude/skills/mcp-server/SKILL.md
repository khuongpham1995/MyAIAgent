---
name: mcp-server
description: >
  Model Context Protocol (MCP) patterns for MyAIAgent. Covers tool registration,
  transport configuration, constant management, MediatR dispatch from tools,
  and testing MCP tools.
  Load when: adding or modifying MCP tools, configuring transport, asked about
  how tools are exposed to Claude or other LLMs.
---

# MCP Server (MyAIAgent)

## Transport Configuration

`Program.cs` wires the MCP server with HTTP transport (SSE + POST):

```csharp
builder.Services
    .AddMcpServer()
    .WithHttpTransport()       // enables GET /mcp/stream (SSE) and POST /mcp
    .WithToolsFromAssembly();  // scans Presentation assembly for [McpServerTool] methods

app.MapMcp();
```

Stdio transport is available but commented out. Activate it for local Claude Desktop integration.

## Tool Registration Pattern

Every MCP tool method follows this pattern:

```csharp
namespace MyAIAgent.McpServer.Presentation.McpTools;

public sealed class CustomerTool(ISender sender)
{
    [McpServerTool(
        Name = McpServerConstant.CustomerTool.GetAllCustomers,
        Description = McpServerConstant.CustomerTool.GetAllCustomersDescription)]
    public async Task<string> GetAllCustomersAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetCustomersQuery(), ct);
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value)
            : $"Error: {string.Join(", ", result.Errors)}";
    }
}
```

**Rules:**
- Tool name and description are constants in `McpServerConstant` (Domain layer)
- Tools are `sealed` classes — never static
- Tools receive `ISender` via primary constructor, not `IMediator`
- Tools return `string` (JSON-serialized payload or error message)
- Tools always accept and propagate `CancellationToken`
- Tools never call infrastructure directly — they always go through MediatR

## Constant Management

All tool names and descriptions live in `McpServerConstant.cs` (Domain):

```csharp
namespace MyAIAgent.McpServer.Domain.Constants;

public static class McpServerConstant
{
    public static class CustomerTool
    {
        public const string CreateCustomer = "create_customer";
        public const string CreateCustomerDescription = "Creates a new customer with name, address, phone, and email.";

        public const string GetAllCustomers = "get_all_customers";
        public const string GetAllCustomersDescription = "Returns a list of all customers.";

        public const string GetCustomersByName = "get_customers_by_name";
        public const string GetCustomersByNameDescription = "Filters customers by name (case-insensitive partial match).";

        public const string UpdateCustomer = "update_customer";
        public const string UpdateCustomerDescription = "Updates an existing customer record by ID.";

        public const string DeleteCustomer = "delete_customer";
        public const string DeleteCustomerDescription = "Deletes a customer by ID.";
    }

    public static class DocumentTool
    {
        public const string AskBackendStructure = "ask_backend_structure";
        public const string AskBackendStructureDescription = "Answers questions about the backend architecture using the backend_structure.md document.";

        public const string AskBackendTests = "ask_backend_tests";
        public const string AskBackendTestsDescription = "Answers questions about unit testing practices using the backend_tests.md document.";
    }
}
```

## Tool Input — Parameters

MCP tool parameters are method parameters annotated with `[Description]`:

```csharp
[McpServerTool(Name = McpServerConstant.CustomerTool.GetCustomersByName)]
public async Task<string> GetCustomersByNameAsync(
    [Description("The name or partial name to search for")] string name,
    CancellationToken ct)
{
    var result = await sender.Send(new GetCustomersByNameQuery(name), ct);
    return JsonSerializer.Serialize(result.Value);
}
```

## Tool Output — Response Format

Return a JSON string for success, a plain error message for failure:

```csharp
return result.IsSuccess
    ? JsonSerializer.Serialize(result.Value)
    : $"Failed: {string.Join("; ", result.Errors)}";
```

## Testing MCP Tools

Test the underlying handler, not the tool wrapper directly. The tool is a thin dispatcher.

```csharp
// Test the handler
[Fact]
public async Task Handle_ValidQuery_ReturnsCustomerList()
{
    var mockClient = new Mock<IApiClient<Customer>>();
    mockClient.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync([new Customer { Id = Guid.NewGuid(), Name = "Alice" }]);
    var handler = new GetCustomersHandler(mockClient.Object);

    var result = await handler.Handle(new GetCustomersQuery(), CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    result.Value.Should().ContainSingle(c => c.Name == "Alice");
}
```

## Anti-patterns

```csharp
// DON'T — call infrastructure directly from tool
public async Task<string> GetAllCustomersAsync(IApiClient<Customer> client, CancellationToken ct)
{
    var customers = await client.GetAllAsync(ct); // bypasses MediatR pipeline
    ...
}

// DON'T — hardcode tool name inline
[McpServerTool(Name = "get_all_customers")]  // use constant instead

// DON'T — drop CancellationToken
public async Task<string> GetAllCustomersAsync()  // missing CancellationToken
```
