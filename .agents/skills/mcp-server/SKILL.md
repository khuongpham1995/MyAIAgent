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
- `app.MapMcp("/mcp")` — maps the primary routes; `app.MapMcp()` also retains the original root endpoint. Both are protected by service-key authorization middleware before protocol dispatch, including unsupported transport methods. Health endpoints remain anonymous.

The checked-in startup configures HTTP only. Stdio would require a separate transport change. Service authorization is enabled by default and cannot be disabled outside Development; active and rotation keys are required when enabled.

Startup calls `UseMcpServiceAuthorization()` before `MapMcpServiceEndpoints()`.
The mapping extension accepts and returns `IEndpointRouteBuilder`; middleware remains a
`WebApplication` extension because raw SDK transport delegates must be authorized before dispatch.
Keep both calls together in hosts and transport test fixtures.

Gateway and Orchestrator use factory-managed clients with `McpServiceKeyHandler`. The key is scoped to the exact configured `McpServer:BaseUrl` origin and the root or `/mcp` paths. Production requires HTTPS; local Development permits loopback HTTP. Redirects and cookies are disabled. Trusted calls use Streamable HTTP explicitly; external agent endpoints remain anonymous and retain transport auto-detection. Authentication rejection must not silently fall back to chat without tools.

## Project Structure

```
McpServer.Domain/
  Constants/McpServerConstant.cs   ← all tool names and descriptions

McpServer.Application/
  Search/Queries/WebSearch/       ← query + handler + validator for web search

McpServer.Presentation/
  McpTools/[Entity]Tool.cs         ← tool class; ISender for validated application operations
```

## Tool Registration Rules

See `.agents/rules/dotnet/mcp.md` for the full pattern (constants, tool class structure, ISender dispatch, return type).

Key invariants:
- Tool names and descriptions are **constants** in `McpServerConstant.cs`
- Both `web_search` and `read_webpage` dispatch through `ISender`; tools never call infrastructure directly.
- Tool methods return `string`: serialized search arrays or a webpage object containing `Url`, `Content`, `RetrievedAtUtc`, and `Truncated`.
- Both tools accept and propagate `CancellationToken`. Current UTC time comes from Orchestrator chat context, not an MCP tool.

## Constant Structure

`McpServerConstant` declares web search and webpage reading names, descriptions, parameters, and safe errors. See `McpServer.Domain/Constants/McpServerConstant.cs` for the current structure.

## Testing

Test the **handler**, which contains the business logic. Cover both tool registrations, removed-tool rejection, and unauthorized calls through transport integration tests using mocked providers. See `.agents/rules/common/testing.md` for test structure and naming conventions.

## Web research boundary

`read_webpage` accepts one public HTTPS URL without credentials, queries, IP literals, local names, or nonstandard ports. Fragments are removed; DNS must resolve only to public addresses. The factory-managed client calls only Tavily `/extract`, reusing `Tavily:ApiKey`, with basic depth, Markdown, no images, no retries, a 20-second deadline including DNS/body, and a 1 MiB response cap. Markdown is bounded to 8,000 characters. Tavily performs remote fetching; no direct fetch fallback exists. HTTP 200 extraction failures remain errors.

Agents expose only named, enabled tools; empty or fully disabled lists expose none. First-party tools share a three-invocation limit per turn, including failures. Orchestrator records successful source URLs and appends a bounded Retrieved sources footer to completed replies before persistence. External tools do not contribute to it. Treat all web content as untrusted evidence.
