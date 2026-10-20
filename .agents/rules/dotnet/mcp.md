---
alwaysApply: true
description: >
  MCP tool registration conventions for MyAIAgent. Covers constants placement,
  tool class structure, MediatR dispatch, and what belongs in each layer.
---

# MCP Tool Registration

## Layer Responsibilities

| Layer | Responsibility |
|---|---|
| Domain | Tool name/description constants in `McpServerConstant.cs` |
| Application | Request, handler, and validator for application operations; web search currently uses `IRequest<string>` |
| Presentation | `[McpServerTool]` methods dispatch application operations via `ISender`; the clock-only tool reads `TimeProvider` directly |

## Constants

All tool names, descriptions, and parameter descriptions belong in `McpServerConstant.cs` in the Domain layer. Never hardcode a tool name or description inline in a tool method attribute.

## Tool Class

- `[McpServerToolType]` on the class, `[McpServerTool]` on each method
- Dispatch application operations through `ISender`; the clock-only date/time tool uses `TimeProvider`. Tools never reference infrastructure directly.
- Keep application validation, mapping, and business logic in handlers and validators. The clock-only tool reads and formats UTC time directly.
- This project returns `string` from its tool methods: serialized search data or a plain text result.
- Tool methods that perform asynchronous work or I/O accept and propagate `CancellationToken`. The synchronous clock-only tool does not need one.

## Anti-patterns

- Never inject `IApiClient<T>` or any infrastructure type into a tool. Dispatch application operations through MediatR; the clock-only tool uses `TimeProvider` directly.
- Never put validation or business logic in a tool method — it belongs in the handler/validator
- Never hardcode tool names in attributes — always reference `McpServerConstant`
