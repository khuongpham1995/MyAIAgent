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

Stdio transport is available but commented out. Activate for local Claude Desktop integration.

## Project Structure

```
McpServer.Domain/
  Constants/McpServerConstant.cs   ← all tool names and descriptions

McpServer.Application/
  Handlers/[Entity]/               ← IRequest + handler + validator per tool

McpServer.Presentation/
  McpTools/[Entity]Tool.cs         ← [McpServerToolType] class, dispatches via ISender
```

## Tool Registration Rules

See `.agents/rules/dotnet/mcp.md` for the full pattern (constants, tool class structure, ISender dispatch, return type).

Key invariants:
- Tool names and descriptions are **constants** in `McpServerConstant.cs`
- Tool class injects `ISender` only — never infrastructure directly
- Tool methods return `string` — JSON-serialized on success, plain message on failure
- All tool methods accept and propagate `CancellationToken`

## Constant Structure

`McpServerConstant` uses nested static classes per entity — one nested class per tool group (e.g., `CustomerTool`, `DocumentTool`). Each nested class holds the tool name constant, description constant, and any parameter description constants. See `McpServer.Domain/Constants/McpServerConstant.cs` for the current structure.

## Testing

Test the **handler**, not the tool wrapper. The tool is a thin dispatcher — the handler contains the business logic. See `.agents/rules/common/testing.md` for test structure and naming conventions.
