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

## Transport

`Program.cs` wires Streamable HTTP transport:
- `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()` — scans for `[McpServerTool]` methods
- `app.MapMcp("/mcp")` — maps the primary routes; `app.MapMcp()` also retains the original root endpoint. Both inherit the shared endpoint authorization filter.

The checked-in startup configures HTTP only. Stdio would require a separate transport change.

## Project Structure

```
McpServer.Domain/
  Constants/McpServerConstant.cs   ← all tool names and descriptions

McpServer.Application/
  Search/Queries/WebSearch/       ← query + handler + validator for web search

McpServer.Presentation/
  McpTools/[Entity]Tool.cs         ← tool class; ISender for application operations, TimeProvider for the clock
```

## Tool Registration Rules

See `.agents/rules/dotnet/mcp.md` for the full pattern (constants, tool class structure, ISender dispatch, return type).

Key invariants:
- Tool names and descriptions are **constants** in `McpServerConstant.cs`
- Web-search tools dispatch through `ISender`; the clock-only date/time tool uses `TimeProvider`. Neither calls infrastructure directly.
- Tool methods return `string`: web search serializes successful results as JSON; the clock tool returns a UTC timestamp.
- Tool methods that perform asynchronous work or I/O accept and propagate `CancellationToken`. The synchronous clock-only tool does not need one.

## Constant Structure

`McpServerConstant` declares constants for web search, UTC date/time, their parameter descriptions, and the search error. See `McpServer.Domain/Constants/McpServerConstant.cs` for the current structure.

## Testing

For tools backed by application operations, test the **handler**, which contains the business logic. Cover MCP registration and direct tools such as `DateTimeTool` through transport integration tests. See `.agents/rules/common/testing.md` for test structure and naming conventions.
